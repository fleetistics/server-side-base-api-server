using api_server.Controllers.Menu.Services;
using api_server.Controllers.Roles.Dto;
using db_model.AppStructure;
using db_model.Companies;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Roles.Services
{
	public enum RoleAdminOutcome
	{
		Done,
		/// <summary>No such role, or one that belongs to another company.</summary>
		RoleNotFound,
		/// <summary>The role to copy the access rights from is not one the company can use.</summary>
		SourceRoleNotFound,
		/// <summary>The name is empty or longer than allowed.</summary>
		InvalidName,
		/// <summary>Another role the company can use already has the name.</summary>
		NameTaken,
		/// <summary>A role every company uses: it can be given access rights and members, but not renamed or deleted.</summary>
		SystemRole,
		/// <summary>Users still have the role.</summary>
		HasMembers,
		/// <summary>The caller is in the list: nobody can change their own role here.</summary>
		CannotChangeSelf,
		/// <summary>A user in the list is not a member of the company.</summary>
		UserNotFound,
		/// <summary>A user in the list was suspended by the site admin: nobody else may change them.</summary>
		MemberNotEditable,
	}

	public interface IRoleAdminService
	{
		/// <summary>A new role of the company; with <see cref="CreateRoleDto.CopyFromRoleId"/> it starts with that role's access rights.</summary>
		Task<(RoleAdminOutcome Outcome, int? RoleId)> CreateAsync(int companyId, CreateRoleDto request, CancellationToken cancellationToken);

		/// <summary>Renames and/or re-describes a role of the company.</summary>
		Task<RoleAdminOutcome> UpdateAsync(int companyId, int roleId, UpdateRoleDto request, CancellationToken cancellationToken);

		/// <summary>Deletes a role of the company that nobody has, with its access rights.</summary>
		Task<RoleAdminOutcome> DeleteAsync(int companyId, int roleId, CancellationToken cancellationToken);

		/// <summary>The company's users (deleted ones left out) with their roles.</summary>
		Task<IReadOnlyList<RoleUserDto>> ListUsersAsync(int companyId, int currentUserId, CancellationToken cancellationToken);

		/// <summary>Gives the role to these users of the company, replacing the role each had.</summary>
		Task<RoleAdminOutcome> AssignUsersAsync(int companyId, int currentUserId, int roleId, IReadOnlyCollection<int> userIds, CancellationToken cancellationToken);
	}

	public sealed class RoleAdminService : IRoleAdminService
	{
		public RoleAdminService(IRepository repository)
		{
			mRepository = repository;
		}

		public async Task<(RoleAdminOutcome Outcome, int? RoleId)> CreateAsync(int companyId, CreateRoleDto request, CancellationToken cancellationToken)
		{
			var name = RoleAdminRules.NormalizeName(request.Name);
			if (!RoleAdminRules.IsValidName(name)) return (RoleAdminOutcome.InvalidName, null);

			var usable = await UsableRolesAsync(companyId, cancellationToken);
			if (RoleAdminRules.IsNameTaken(usable, name!)) return (RoleAdminOutcome.NameTaken, null);
			if (request.CopyFromRoleId is int source && usable.All(r => r.Id != source)) return (RoleAdminOutcome.SourceRoleNotFound, null);

			// The role and its copied rights are one unit: either both exist or neither. The role's id is needed for the rights, so it
			// takes two saves inside one transaction. An explicit transaction has to run inside the execution strategy (connection
			// retry is enabled), which may re-run the whole block, hence the cleared tracker and fresh entities on every attempt.
			var db = mRepository.DbContext;
			var roleId = 0;
			await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
			{
				db.ChangeTracker.Clear();
				await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

				var role = new PermissionRole { Name = name!, Description = RoleAdminRules.NormalizeDescription(request.Description), CompanyId = companyId };
				mRepository.Create(role);
				await mRepository.SaveAsync(cancellationToken);

				if (request.CopyFromRoleId is int from)
				{
					var menuItemIds = await mRepository.GetQueryable<ApplicationMenuItem>(i => i.MenuId == MenuService.MainMenuId)
						.AsNoTracking().Select(i => i.Id).ToListAsync(cancellationToken);
					// What the source gives the company today (its override when it has one, else the defaults) becomes the new role's own rows.
					var sourceRows = await mRepository.GetQueryable<Role2MenuItem>(g => g.PermissionRoleId == from && menuItemIds.Contains(g.ApplicationMenuItemId) && (g.CompanyId == null || g.CompanyId == companyId))
						.AsNoTracking().ToListAsync(cancellationToken);
					foreach (var right in RoleMenuPermissionRules.Effective(sourceRows, companyId))
					{
						mRepository.Create(new Role2MenuItem
						{
							PermissionRoleId = role.Id,
							CompanyId = companyId,
							ApplicationMenuItemId = right.ApplicationMenuItemId,
							CanView = right.CanView,
							CanCreate = right.CanCreate,
							CanEdit = right.CanEdit,
							CanDelete = right.CanDelete,
						});
					}
					await mRepository.SaveAsync(cancellationToken);
				}

				await transaction.CommitAsync(cancellationToken);
				roleId = role.Id;
			});

			return (RoleAdminOutcome.Done, roleId);
		}

		public async Task<RoleAdminOutcome> UpdateAsync(int companyId, int roleId, UpdateRoleDto request, CancellationToken cancellationToken)
		{
			var usable = await UsableRolesAsync(companyId, cancellationToken);
			var role = usable.SingleOrDefault(r => r.Id == roleId);
			if (role == null) return RoleAdminOutcome.RoleNotFound;
			if (role.CompanyId == null) return RoleAdminOutcome.SystemRole;

			if (request.Name != null)
			{
				var name = RoleAdminRules.NormalizeName(request.Name);
				if (!RoleAdminRules.IsValidName(name)) return RoleAdminOutcome.InvalidName;
				if (RoleAdminRules.IsNameTaken(usable, name!, exceptRoleId: roleId)) return RoleAdminOutcome.NameTaken;
				role.Name = name!;
			}
			if (request.Description != null) role.Description = RoleAdminRules.NormalizeDescription(request.Description);

			await mRepository.SaveAsync(cancellationToken);
			return RoleAdminOutcome.Done;
		}

		public async Task<RoleAdminOutcome> DeleteAsync(int companyId, int roleId, CancellationToken cancellationToken)
		{
			var usable = await UsableRolesAsync(companyId, cancellationToken);
			var role = usable.SingleOrDefault(r => r.Id == roleId);
			if (role == null) return RoleAdminOutcome.RoleNotFound;
			if (role.CompanyId == null) return RoleAdminOutcome.SystemRole;

			// Every membership counts, whatever its status: the database does not let a role in use go.
			if (await mRepository.GetQueryable<User2Company>(m => m.PermissionRoleId == roleId).AnyAsync(cancellationToken)) return RoleAdminOutcome.HasMembers;

			var rights = await mRepository.GetQueryable<Role2MenuItem>(g => g.PermissionRoleId == roleId).ToListAsync(cancellationToken);
			mRepository.DeleteAll(rights);
			mRepository.Delete(role);
			await mRepository.SaveAsync(cancellationToken);
			return RoleAdminOutcome.Done;
		}

		public async Task<IReadOnlyList<RoleUserDto>> ListUsersAsync(int companyId, int currentUserId, CancellationToken cancellationToken)
		{
			var memberships = (await mRepository.GetQueryable<User2Company>(m => m.CompanyId == companyId)
					.AsNoTracking().ToListAsync(cancellationToken))
				.Where(m => !RoleAdminRules.IsDeleted(m.StatusId))
				.ToList();
			var userIds = memberships.Select(m => m.UserId).ToList();
			var users = await mRepository.GetQueryable<User>(u => userIds.Contains(u.Id))
				.AsNoTracking()
				.Select(u => new { u.Id, u.DisplayName, u.FullName, u.UserName, u.Email })
				.ToDictionaryAsync(u => u.Id, cancellationToken);

			return memberships
				.Where(m => users.ContainsKey(m.UserId))
				.Select(m =>
				{
					var u = users[m.UserId];
					return new RoleUserDto
					{
						UserId = m.UserId,
						DisplayName = !string.IsNullOrWhiteSpace(u.DisplayName) ? u.DisplayName : !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : u.UserName,
						Email = u.Email,
						StatusId = m.StatusId,
						RoleId = m.PermissionRoleId,
						IsCurrentUser = m.UserId == currentUserId,
					};
				})
				.OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
				.ThenBy(x => x.UserId)
				.ToList();
		}

		public async Task<RoleAdminOutcome> AssignUsersAsync(int companyId, int currentUserId, int roleId, IReadOnlyCollection<int> userIds, CancellationToken cancellationToken)
		{
			var usable = await UsableRolesAsync(companyId, cancellationToken);
			if (usable.All(r => r.Id != roleId)) return RoleAdminOutcome.RoleNotFound;

			var ids = userIds.Distinct().ToList();
			if (ids.Contains(currentUserId)) return RoleAdminOutcome.CannotChangeSelf;
			if (ids.Count == 0) return RoleAdminOutcome.Done;

			var memberships = (await mRepository.GetQueryable<User2Company>(m => m.CompanyId == companyId && ids.Contains(m.UserId))
					.ToListAsync(cancellationToken))
				.Where(m => !RoleAdminRules.IsDeleted(m.StatusId))
				.ToList();
			if (memberships.Count != ids.Count) return RoleAdminOutcome.UserNotFound;
			if (memberships.Any(m => RoleAdminRules.IsLockedBySiteAdmin(m.StatusId))) return RoleAdminOutcome.MemberNotEditable;

			foreach (var membership in memberships) membership.PermissionRoleId = roleId;
			await mRepository.SaveAsync(cancellationToken);
			return RoleAdminOutcome.Done;
		}

		/// <summary>The roles the company can use (every company's and its own), tracked so a rename can be saved.</summary>
		private Task<List<PermissionRole>> UsableRolesAsync(int companyId, CancellationToken cancellationToken) =>
			mRepository.GetQueryable<PermissionRole>(r => r.CompanyId == null || r.CompanyId == companyId).ToListAsync(cancellationToken);

		private readonly IRepository mRepository;
	}
}
