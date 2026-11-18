using api_server.Controllers.Companies.Dto;
using api_server.Users;
using db_model.Companies;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using exs.modelCommons.UserManagement;
using Microsoft.EntityFrameworkCore;

namespace api_server.Companies
{
	/// <summary>
	/// Read access to everything that belongs to one company, for that company's admins.
	/// Every method is scoped to the <c>companyId</c> passed in (the caller's current company).
	/// </summary>
	public interface ICompanyAdminService
	{
		/// <summary>Null when the company does not exist.</summary>
		Task<CompanyDto?> GetCompanyAsync(int companyId, CancellationToken cancellationToken);

		/// <summary>Active users whose CompanyId is the company (delegates to <see cref="IUserService"/>).</summary>
		Task<IReadOnlyList<UserDto>> GetActiveCompanyUsersAsync(int companyId, CancellationToken cancellationToken);

		/// <summary>
		/// Users related to the company without being active members: those with a non-active User2Company
		/// status plus those with a user join request to the company (any request status).
		/// </summary>
		Task<IReadOnlyList<UserDto>> GetNotActiveCompanyRelatedUsersAsync(int companyId, CancellationToken cancellationToken);

		Task<IReadOnlyList<User2CompanyDto>> GetUser2CompanyAsync(int companyId, CancellationToken cancellationToken);

		/// <summary>
		/// One session per user of the company: the latest (by LatestUpdate) active session or, for a user
		/// without an active session, the latest session of any status. Users without sessions are omitted.
		/// </summary>
		//Task<IReadOnlyList<LatestUserSessionDto>> GetLatestUserSessionsAsync(int companyId, CancellationToken cancellationToken);

		/// <summary>System-wide roles (CompanyId null) plus the company's own.</summary>
		Task<IReadOnlyList<PermissionRoleDto>> GetPermissionRolesAsync(int companyId, CancellationToken cancellationToken);

		Task<IReadOnlyList<UserGroupDto>> GetUserGroupsAsync(int companyId, CancellationToken cancellationToken);

		Task<IReadOnlyList<User2GroupDto>> GetUser2GroupsAsync(int companyId, CancellationToken cancellationToken);

		Task<IReadOnlyList<UserJoinInvitationDto>> GetUserJoinInvitationsAsync(int companyId, CancellationToken cancellationToken);

		Task<IReadOnlyList<UserJoinRequestDto>> GetUserJoinRequestsAsync(int companyId, CancellationToken cancellationToken);

        Task<CompanyUserManagementContextDto> GetUserManagementContextAsync(int companyId, CancellationToken cancellationToken);

		/// <summary>
		/// Applies the fields present in <paramref name="patch"/> to the user's membership in the company; the
		/// whole patch is checked first and saved in one go, so it is applied entirely or not at all.
		/// A company admin can only set the status to Active (reactivate a Suspended member), Suspended (from Active,
		/// WithOtherCompany or Left) or Deleted (from any of those and Suspended), and the role to a system-wide
		/// one or the company's own. Rows already Deleted or handled by the site admin are not editable.
		/// </summary>
		Task<CompanyAdminPatchOutcome> PatchUser2CompanyAsync(int companyId, int currentUserId, int userId, User2CompanyPatchDto patch, CancellationToken cancellationToken);

		/// <summary>
		/// Adds the user to and removes the user from groups of the company (see <see cref="User2GroupPatchDto"/>).
		/// NotFound when the user is not a (non-deleted) member of the company.
		/// </summary>
		Task<CompanyAdminPatchOutcome> PatchUser2GroupsAsync(int companyId, int userId, User2GroupPatchDto patch, CancellationToken cancellationToken);

		/// <summary>
		/// Creates a group in the company, optionally with its first members. Outcome is <see cref="CompanyAdminPatchOutcome.Updated"/>
		/// (with the group), <see cref="CompanyAdminPatchOutcome.NameTaken"/> or <see cref="CompanyAdminPatchOutcome.UserNotAllowed"/>.
		/// </summary>
		Task<(CompanyAdminPatchOutcome Outcome, UserGroupDto? Group)> CreateUserGroupAsync(int companyId, UserGroupCreateDto body, CancellationToken cancellationToken);

		/// <summary>
		/// Applies the fields present in <paramref name="patch"/> to the group and adds/removes its members, all saved in one go.
		/// NotFound when the group is not one of the company's.
		/// </summary>
		Task<CompanyAdminPatchOutcome> PatchUserGroupAsync(int companyId, int groupId, UserGroupPatchDto patch, CancellationToken cancellationToken);

		/// <summary>Deletes the group together with its memberships. NotFound when the group is not one of the company's.</summary>
		Task<CompanyAdminPatchOutcome> DeleteUserGroupAsync(int companyId, int groupId, CancellationToken cancellationToken);
    }

	public enum CompanyAdminPatchOutcome
	{
		Updated,
		NotFound,
		/// <summary>The caller tried to change their own membership.</summary>
		SelfNotAllowed,
		/// <summary>The target status is not one a company admin can set.</summary>
		StatusNotAllowed,
		/// <summary>The current status does not allow the change.</summary>
		TransitionNotAllowed,
		/// <summary>The role does not exist or belongs to another company.</summary>
		RoleNotAllowed,
		/// <summary>A group to add does not exist in the company.</summary>
		GroupNotAllowed,
		/// <summary>A user to add is not a (non-deleted) member of the company.</summary>
		UserNotAllowed,
		/// <summary>Another group of the company already has this name.</summary>
		NameTaken,
	}

	public sealed class CompanyAdminService : ICompanyAdminService
	{
		private readonly IRepository mRepository;
		private readonly IUserService mUsers;

		public CompanyAdminService(IRepository repository, IUserService users)
		{
			mRepository = repository;
			mUsers = users;
		}
        public async Task<CompanyUserManagementContextDto> GetUserManagementContextAsync(int companyId, CancellationToken cancellationToken)
		{
            var result = new CompanyUserManagementContextDto();
            // Current time of the DB server, read before the queries so nothing loaded below is newer than it;
            // converted here to unix milliseconds (rounded down).
            var dbNow = await mRepository.DbContext.Database
                .SqlQueryRaw<DateTime>("select now() as \"Value\"")
                .SingleAsync(cancellationToken);
            result.LatestUpdate = new DateTimeOffset(dbNow.ToUniversalTime()).ToUnixTimeMilliseconds();

            // Deleted members are gone as far as the admin UI is concerned.
            result.User2Companies = await mRepository.GetQueryable<User2Company>(x => x.CompanyId == companyId && x.StatusId != User2CompanyStatus.Deleted && x.StatusId != User2CompanyStatus.DeletedBySiteAdmin).Select(x => new User2CompanyDto(x)).ToListAsync(cancellationToken);
            var companyUserIds = result.User2Companies.Select(x => x.UserId).ToHashSet();
            result.LatestUserActivities = await mRepository.GetQueryable<UserSession>(x => companyUserIds.Contains(x.UserId)).Select(x => new LatestUserActivityDto(x)).ToListAsync(cancellationToken);
            result.PermissionRoles = await mRepository.GetQueryable<PermissionRole>(x => x.CompanyId == null || x.CompanyId == companyId).Select(x => new PermissionRoleDto(x)).ToListAsync(cancellationToken);

			result.UserGroups = await mRepository.GetQueryable<UserGroup>(x => x.CompanyId == companyId).Select(x => new UserGroupDto(x)).ToListAsync(cancellationToken);
            result.User2Groups = await mRepository.GetQueryable<User2Group>(x => x.CompanyId == companyId).Select(x => new User2GroupDto(x)).ToListAsync(cancellationToken);
            result.UserJoinInvitations = await mRepository.GetQueryable<UserJoinInvitation>(x => x.CompanyId == companyId).Select(x => new UserJoinInvitationDto(x)).ToListAsync(cancellationToken);
			if (result.UserJoinInvitations != null) companyUserIds.UnionWith(result.UserJoinInvitations.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value));
            result.UserJoinRequests = await mRepository.GetQueryable<UserJoinRequest>(x => x.CompanyId == companyId).Select(x => new UserJoinRequestDto(x)).ToListAsync(cancellationToken);
			if  (result.UserJoinRequests != null) companyUserIds.UnionWith(result.UserJoinRequests.Select(x => x.UserId));
            result.Users = (await mUsers.GetUsersByIdsAsync(companyUserIds, cancellationToken)).ToList();

            return result;
        }



        public Task<IReadOnlyList<UserDto>> GetActiveCompanyUsersAsync(int companyId, CancellationToken cancellationToken) =>
			mUsers.GetActiveCompanyUsersAsync(companyId, cancellationToken);

		public async Task<IReadOnlyList<UserDto>> GetNotActiveCompanyRelatedUsersAsync(int companyId, CancellationToken cancellationToken)
		{
			var notActiveMemberIds = await mRepository
				.GetQueryable<User2Company>(x => x.CompanyId == companyId && x.StatusId != User2CompanyStatus.Active)
				.Select(x => x.UserId)
				.ToListAsync(cancellationToken);

			var requesterIds = await mRepository
				.GetQueryable<UserJoinRequest>(x => x.CompanyId == companyId)
				.Select(x => x.UserId)
				.ToListAsync(cancellationToken);

			var userIds = notActiveMemberIds.Union(requesterIds).ToList();
			return await mUsers.GetUsersByIdsAsync(userIds, cancellationToken);
		}

		public async Task<CompanyDto?> GetCompanyAsync(int companyId, CancellationToken cancellationToken)
		{
			var company = await mRepository.GetQueryable<Company>(c => c.Id == companyId)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			return company == null ? null : new CompanyDto(company);
		}

		public async Task<IReadOnlyList<User2CompanyDto>> GetUser2CompanyAsync(int companyId, CancellationToken cancellationToken)
		{
			var items = await mRepository.GetQueryable<User2Company>(x => x.CompanyId == companyId && x.StatusId != User2CompanyStatus.Deleted && x.StatusId != User2CompanyStatus.DeletedBySiteAdmin)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return items.Select(x => new User2CompanyDto(x)).ToList();
		}

		public async Task<CompanyAdminPatchOutcome> PatchUser2CompanyAsync(int companyId, int currentUserId, int userId, User2CompanyPatchDto patch, CancellationToken cancellationToken)
		{
			if (userId == currentUserId) return CompanyAdminPatchOutcome.SelfNotAllowed;

			// Validation guarantees a set field has a value.
			short? newStatusId = patch.StatusId.IsSet ? patch.StatusId.Value : null;
			int? newRoleId = patch.PermissionRoleId.IsSet ? patch.PermissionRoleId.Value : null;
			if (newStatusId is { } s && s != User2CompanyStatus.Active && s != User2CompanyStatus.Suspended && s != User2CompanyStatus.Deleted)
				return CompanyAdminPatchOutcome.StatusNotAllowed;

			var item = await mRepository.GetQueryable<User2Company>(x => x.CompanyId == companyId && x.UserId == userId)
				.SingleOrDefaultAsync(cancellationToken);
			if (item == null || item.StatusId == User2CompanyStatus.Deleted || item.StatusId == User2CompanyStatus.DeletedBySiteAdmin)
				return CompanyAdminPatchOutcome.NotFound;
			// Handled by the site admin: not editable at all.
			if (item.StatusId == User2CompanyStatus.SuspendedBySiteAdmin) return CompanyAdminPatchOutcome.TransitionNotAllowed;

			if (newStatusId is { } statusId)
			{
				var allowed = statusId switch
				{
					User2CompanyStatus.Active => item.StatusId == User2CompanyStatus.Suspended,
					User2CompanyStatus.Suspended => item.StatusId is User2CompanyStatus.Active or User2CompanyStatus.WithOtherCompany or User2CompanyStatus.Left,
					_ => item.StatusId is User2CompanyStatus.Active or User2CompanyStatus.WithOtherCompany or User2CompanyStatus.Left or User2CompanyStatus.Suspended,
				};
				if (!allowed) return CompanyAdminPatchOutcome.TransitionNotAllowed;
			}

			if (newRoleId is { } roleId)
			{
				var roleUsable = await mRepository.GetQueryable<PermissionRole>(r => r.Id == roleId && (r.CompanyId == null || r.CompanyId == companyId))
					.AnyAsync(cancellationToken);
				if (!roleUsable) return CompanyAdminPatchOutcome.RoleNotAllowed;
			}

			if (newStatusId is { } status) item.StatusId = status;
			if (newRoleId is { } role) item.PermissionRoleId = role;
			await mRepository.SaveAsync(cancellationToken);
			return CompanyAdminPatchOutcome.Updated;
		}

		public async Task<CompanyAdminPatchOutcome> PatchUser2GroupsAsync(int companyId, int userId, User2GroupPatchDto patch, CancellationToken cancellationToken)
		{
			var isMember = await mRepository
				.GetQueryable<User2Company>(x => x.CompanyId == companyId && x.UserId == userId && x.StatusId != User2CompanyStatus.Deleted && x.StatusId != User2CompanyStatus.DeletedBySiteAdmin)
				.AnyAsync(cancellationToken);
			if (!isMember) return CompanyAdminPatchOutcome.NotFound;

			var add = patch.AddGroupIds?.Distinct().ToList() ?? [];
			var remove = patch.RemoveGroupIds?.Distinct().ToList() ?? [];

			// Groups can only be joined within the company; leaving one that no longer exists is simply a no-op.
			if (add.Count > 0)
			{
				var companyGroupCount = await mRepository.GetQueryable<UserGroup>(g => g.CompanyId == companyId && add.Contains(g.Id)).CountAsync(cancellationToken);
				if (companyGroupCount != add.Count) return CompanyAdminPatchOutcome.GroupNotAllowed;
			}

			var memberships = await mRepository
				.GetQueryable<User2Group>(x => x.CompanyId == companyId && x.UserId == userId && (add.Contains(x.GroupId) || remove.Contains(x.GroupId)))
				.ToListAsync(cancellationToken);

			foreach (var groupId in add.Where(id => memberships.All(m => m.GroupId != id)))
			{
				mRepository.Create(new User2Group { CompanyId = companyId, UserId = userId, GroupId = groupId });
			}
			mRepository.DeleteAll(memberships.Where(m => remove.Contains(m.GroupId)).ToList());

			await mRepository.SaveAsync(cancellationToken);
			return CompanyAdminPatchOutcome.Updated;
		}

		public async Task<(CompanyAdminPatchOutcome Outcome, UserGroupDto? Group)> CreateUserGroupAsync(int companyId, UserGroupCreateDto body, CancellationToken cancellationToken)
		{
			var name = body.Name.Trim();
			if (await GroupNameTakenAsync(companyId, name, null, cancellationToken)) return (CompanyAdminPatchOutcome.NameTaken, null);

			var add = body.AddUserIds?.Distinct().ToList() ?? [];
			if (!await AreCompanyMembersAsync(companyId, add, cancellationToken)) return (CompanyAdminPatchOutcome.UserNotAllowed, null);

			var group = new UserGroup { CompanyId = companyId, Name = name, Description = NullIfBlank(body.Description), GroupColor = body.GroupColor };
			mRepository.Create(group);
			// The id is only known once the group is saved; the memberships follow in the same unit of work.
			await mRepository.SaveAsync(cancellationToken);
			foreach (var userId in add) mRepository.Create(new User2Group { CompanyId = companyId, UserId = userId, GroupId = group.Id });
			if (add.Count > 0) await mRepository.SaveAsync(cancellationToken);
			return (CompanyAdminPatchOutcome.Updated, new UserGroupDto(group));
		}

		public async Task<CompanyAdminPatchOutcome> PatchUserGroupAsync(int companyId, int groupId, UserGroupPatchDto patch, CancellationToken cancellationToken)
		{
			var group = await mRepository.GetQueryable<UserGroup>(g => g.CompanyId == companyId && g.Id == groupId).SingleOrDefaultAsync(cancellationToken);
			if (group == null) return CompanyAdminPatchOutcome.NotFound;

			var add = patch.AddUserIds?.Distinct().ToList() ?? [];
			var remove = patch.RemoveUserIds?.Distinct().ToList() ?? [];

			// The whole patch is checked before anything is changed.
			string? newName = patch.Name.IsSet ? patch.Name.Value!.Trim() : null;
			if (newName != null && await GroupNameTakenAsync(companyId, newName, groupId, cancellationToken)) return CompanyAdminPatchOutcome.NameTaken;
			// Removing a user that is no longer a member of the company is fine (a no-op or a cleanup); adding one is not.
			if (!await AreCompanyMembersAsync(companyId, add, cancellationToken)) return CompanyAdminPatchOutcome.UserNotAllowed;

			if (newName != null) group.Name = newName;
			if (patch.Description.IsSet) group.Description = NullIfBlank(patch.Description.Value);
			if (patch.GroupColor.IsSet) group.GroupColor = patch.GroupColor.Value;

			if (add.Count > 0 || remove.Count > 0)
			{
				var memberships = await mRepository
					.GetQueryable<User2Group>(x => x.GroupId == groupId && (add.Contains(x.UserId) || remove.Contains(x.UserId)))
					.ToListAsync(cancellationToken);
				foreach (var userId in add.Where(id => memberships.All(m => m.UserId != id)))
				{
					mRepository.Create(new User2Group { CompanyId = companyId, UserId = userId, GroupId = groupId });
				}
				mRepository.DeleteAll(memberships.Where(m => remove.Contains(m.UserId)).ToList());
			}

			await mRepository.SaveAsync(cancellationToken);
			return CompanyAdminPatchOutcome.Updated;
		}

		public async Task<CompanyAdminPatchOutcome> DeleteUserGroupAsync(int companyId, int groupId, CancellationToken cancellationToken)
		{
			var group = await mRepository.GetQueryable<UserGroup>(g => g.CompanyId == companyId && g.Id == groupId).SingleOrDefaultAsync(cancellationToken);
			if (group == null) return CompanyAdminPatchOutcome.NotFound;

			// The user2group rows go with the group (cascade); invitations that preset it just lose the preset (set null).
			mRepository.Delete(group);
			await mRepository.SaveAsync(cancellationToken);
			return CompanyAdminPatchOutcome.Updated;
		}

		private async Task<bool> GroupNameTakenAsync(int companyId, string name, int? exceptGroupId, CancellationToken cancellationToken)
		{
			var lower = name.ToLower();
			return await mRepository.GetQueryable<UserGroup>(g => g.CompanyId == companyId && g.Id != exceptGroupId && g.Name.ToLower() == lower).AnyAsync(cancellationToken);
		}

		/// <summary>True when every id is a (non-deleted) member of the company.</summary>
		private async Task<bool> AreCompanyMembersAsync(int companyId, List<int> userIds, CancellationToken cancellationToken)
		{
			if (userIds.Count == 0) return true;
			var count = await mRepository
				.GetQueryable<User2Company>(x => x.CompanyId == companyId && userIds.Contains(x.UserId) && x.StatusId != User2CompanyStatus.Deleted && x.StatusId != User2CompanyStatus.DeletedBySiteAdmin)
				.CountAsync(cancellationToken);
			return count == userIds.Count;
		}

		private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

		//public async Task<IReadOnlyList<LatestUserSessionDto>> GetLatestUserSessionsAsync(int companyId, CancellationToken cancellationToken)
		//{
		//	var companyUserIds = mRepository.GetQueryable<User>(u => u.CompanyId == companyId).Select(u => u.Id);

		//	var activeSessions = await LatestSessionPerUser(
		//			mRepository.GetQueryable<UserSession>(s => companyUserIds.Contains(s.UserId) && s.StatusId == UserSessionStatus.Active))
		//		.ToListAsync(cancellationToken);

		//	// Only users without an active session fall back to their latest session of any status.
		//	var usersWithActive = activeSessions.Select(s => s.UserId).ToList();
		//	var fallbackSessions = await LatestSessionPerUser(
		//			mRepository.GetQueryable<UserSession>(s => companyUserIds.Contains(s.UserId) && !usersWithActive.Contains(s.UserId)))
		//		.ToListAsync(cancellationToken);

		//	return activeSessions.Concat(fallbackSessions)
		//		.OrderBy(s => s.UserId)
		//		.Select(s => new LatestUserSessionDto(s))
		//		.ToList();
		//}

		public async Task<IReadOnlyList<PermissionRoleDto>> GetPermissionRolesAsync(int companyId, CancellationToken cancellationToken)
		{
			var items = await mRepository.GetQueryable<PermissionRole>(r => r.CompanyId == null || r.CompanyId == companyId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return items.Select(r => new PermissionRoleDto(r)).ToList();
		}

		// "as roles" (see GetPermissionRolesAsync), but UserGroup.CompanyId is required, so there are no system-wide groups.
		public async Task<IReadOnlyList<UserGroupDto>> GetUserGroupsAsync(int companyId, CancellationToken cancellationToken)
		{
			var items = await mRepository.GetQueryable<UserGroup>(g => g.CompanyId == companyId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return items.Select(g => new UserGroupDto(g)).ToList();
		}

		public async Task<IReadOnlyList<User2GroupDto>> GetUser2GroupsAsync(int companyId, CancellationToken cancellationToken)
		{
			var items = await mRepository.GetQueryable<User2Group>(x => x.CompanyId == companyId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return items.Select(x => new User2GroupDto(x)).ToList();
		}

		public async Task<IReadOnlyList<UserJoinInvitationDto>> GetUserJoinInvitationsAsync(int companyId, CancellationToken cancellationToken)
		{
			var items = await mRepository.GetQueryable<UserJoinInvitation>(x => x.CompanyId == companyId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return items.Select(x => new UserJoinInvitationDto(x)).ToList();
		}

		public async Task<IReadOnlyList<UserJoinRequestDto>> GetUserJoinRequestsAsync(int companyId, CancellationToken cancellationToken)
		{
			var items = await mRepository.GetQueryable<UserJoinRequest>(x => x.CompanyId == companyId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return items.Select(x => new UserJoinRequestDto(x)).ToList();
		}

		private static IQueryable<UserSession> LatestSessionPerUser(IQueryable<UserSession> sessions) =>
			sessions.AsNoTracking()
				.GroupBy(s => s.UserId)
				.Select(g => g.OrderByDescending(s => s.LatestUpdate).First());
	}
}
