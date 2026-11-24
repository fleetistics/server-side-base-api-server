using db_model.Companies;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Join.Services
{
	public enum MembershipGrant
	{
		/// <summary>The user is a member now (newly, again, or already was).</summary>
		Granted,
		/// <summary>No role could be found to give: the invitation names none and the company has no default.</summary>
		NoRole,
		/// <summary>The user is suspended in the company, or was removed by the site admin: only the site admin or an admin's own action changes that.</summary>
		Blocked,
	}

	/// <summary>What the join flows share: turning an approved request into a membership, and the checks around it.</summary>
	internal static class JoinMembership
	{
		public static bool IsMember(short statusId) => statusId is User2CompanyStatus.Active or User2CompanyStatus.WithOtherCompany;

		/// <summary>The role to give: the invitation's own, else the company's default one, else the system "Read-Only".</summary>
		public static async Task<int?> ResolveRoleAsync(IRepository repository, int companyId, int? invitedRoleId, CancellationToken cancellationToken)
		{
			if (invitedRoleId is int given)
			{
				var usable = await repository.GetQueryable<PermissionRole>(r => r.Id == given && (r.CompanyId == null || r.CompanyId == companyId)).AnyAsync(cancellationToken);
				if (usable) return given;
			}

			var roles = await repository.GetQueryable<PermissionRole>(r => r.CompanyId == null || r.CompanyId == companyId)
				.AsNoTracking().Select(r => new { r.Id, r.Name, r.CompanyId, r.IsDefault }).ToListAsync(cancellationToken);
			return roles.Where(r => r.IsDefault == true).OrderByDescending(r => r.CompanyId != null).Select(r => (int?)r.Id).FirstOrDefault()
				?? roles.Where(r => r.CompanyId == null && r.Name == "Read-Only").Select(r => (int?)r.Id).FirstOrDefault();
		}

		/// <summary>
		/// Makes the user a member of the company (nothing is saved here; the caller saves). A membership that is gone (deleted, left) comes back
		/// with the new role; one that exists and is active is left as it is; a suspended one is not touched.
		/// </summary>
		public static async Task<MembershipGrant> GrantAsync(IRepository repository, int companyId, int userId, int? invitedRoleId, int? groupId, CancellationToken cancellationToken)
		{
			var existing = await repository.GetQueryable<User2Company>(m => m.CompanyId == companyId && m.UserId == userId).SingleOrDefaultAsync(cancellationToken);
			if (existing != null && IsMember(existing.StatusId)) return MembershipGrant.Granted;
			if (existing != null && existing.StatusId is User2CompanyStatus.Suspended or User2CompanyStatus.SuspendedBySiteAdmin or User2CompanyStatus.DeletedBySiteAdmin) return MembershipGrant.Blocked;

			var roleId = await ResolveRoleAsync(repository, companyId, invitedRoleId, cancellationToken);
			if (roleId == null) return MembershipGrant.NoRole;

			var now = DateTime.UtcNow;
			if (existing == null)
			{
				repository.Create(new User2Company
				{
					CompanyId = companyId,
					UserId = userId,
					PermissionRoleId = roleId.Value,
					StatusId = User2CompanyStatus.Active,
					JoinTypeId = User2CompanyJoinType.Invited,
					JoinedDate = now,
					LatestUpdate = now,
				});
			}
			else
			{
				existing.PermissionRoleId = roleId.Value;
				existing.StatusId = User2CompanyStatus.Active;
				existing.JoinTypeId = User2CompanyJoinType.Invited;
				existing.JoinedDate = now;
			}

			if (groupId is int group)
			{
				var groupInCompany = await repository.GetQueryable<UserGroup>(g => g.Id == group && g.CompanyId == companyId).AnyAsync(cancellationToken);
				var alreadyIn = await repository.GetQueryable<User2Group>(g => g.GroupId == group && g.UserId == userId).AnyAsync(cancellationToken);
				if (groupInCompany && !alreadyIn) repository.Create(new User2Group { CompanyId = companyId, UserId = userId, GroupId = group });
			}

			// A person with no company yet starts in this one.
			var user = await repository.GetQueryable<User>(u => u.Id == userId).SingleOrDefaultAsync(cancellationToken);
			if (user != null && user.CompanyId == null) user.CompanyId = companyId;

			return MembershipGrant.Granted;
		}

		public static bool EmailMatches(string? a, string? b) =>
			!string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
	}
}
