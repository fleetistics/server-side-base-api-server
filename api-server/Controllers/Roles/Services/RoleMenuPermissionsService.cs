using api_server.Controllers.Menu.Services;
using api_server.Controllers.Roles.Dto;
using db_model.AppStructure;
using db_model.Companies;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Roles.Services
{
	public enum SetRoleMenuPermissionsOutcome
	{
		Updated,
		/// <summary>No such role, or one the scope cannot use (another company's, or a company role on the portal-admin screen).</summary>
		RoleNotFound,
		/// <summary>An entry names an item that is not in the menu.</summary>
		UnknownMenuItem,
		/// <summary>Two entries name the same item.</summary>
		DuplicateMenuItem,
	}

	/// <summary>
	/// The access rights of roles on the menu, in one of two scopes. With no company id: the defaults of the system roles (Role2MenuItem
	/// rows with CompanyId null), which every company without rows of its own uses. With a company id: what that company uses, i.e.
	/// its own roles and its overrides of the system roles; an override replaces the defaults of that role as a whole.
	/// </summary>
	public interface IRoleMenuPermissionsService
	{
		/// <summary>
		/// The roles of the scope (system roles only for the defaults; the system roles and the company's own for a company), with what
		/// each may do in the menu there.
		/// </summary>
		Task<IReadOnlyList<RoleSummaryDto>> ListRolesAsync(int? companyId, CancellationToken cancellationToken);

		/// <summary>The whole menu with the role's flags on it; null when the role does not exist in the scope.</summary>
		Task<RoleMenuPermissionsDto?> GetAsync(int? companyId, int roleId, CancellationToken cancellationToken);

		/// <summary>
		/// Replaces the role's flags on the menu in the scope. For the defaults, what the request lists is stored and everything else
		/// is taken away. For a company, every menu item is stored (false for what the request does not list), which makes the rows an override.
		/// </summary>
		Task<SetRoleMenuPermissionsOutcome> SetAsync(int? companyId, int roleId, SetRoleMenuPermissionsDto request, CancellationToken cancellationToken);

		/// <summary>Drops the company's rows for the role, so the role goes back to the defaults (an own role of the company is left with none).</summary>
		Task<SetRoleMenuPermissionsOutcome> ResetAsync(int companyId, int roleId, CancellationToken cancellationToken);
	}

	public sealed class RoleMenuPermissionsService : IRoleMenuPermissionsService
	{
		public RoleMenuPermissionsService(IRepository repository)
		{
			mRepository = repository;
		}

		public async Task<IReadOnlyList<RoleSummaryDto>> ListRolesAsync(int? companyId, CancellationToken cancellationToken)
		{
			var roles = await UsableRoles(companyId)
				.AsNoTracking()
				.OrderBy(r => r.Id)
				.ToListAsync(cancellationToken);

			var menuItemIds = await MenuItemIdsAsync(cancellationToken);
			var grants = await mRepository.GetQueryable<Role2MenuItem>(g => menuItemIds.Contains(g.ApplicationMenuItemId) && (g.CompanyId == null || g.CompanyId == companyId))
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			var members = await mRepository.GetQueryable<User2Company>(m => companyId == null || m.CompanyId == companyId)
				.GroupBy(m => m.PermissionRoleId)
				.Select(g => new { RoleId = g.Key, Count = g.Count() })
				.ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

			return roles.Select(r =>
			{
				var rows = grants.Where(g => g.PermissionRoleId == r.Id).ToList();
				var effective = RoleMenuPermissionRules.Effective(rows, companyId);
				return new RoleSummaryDto
				{
					Id = r.Id,
					Name = r.Name,
					Description = r.Description,
					IsSystem = r.CompanyId == null,
					IsOverridden = companyId is int company && RoleMenuPermissionRules.HasCompanyRows(rows, company),
					MemberCount = members.GetValueOrDefault(r.Id),
					TotalMenus = menuItemIds.Count,
					ViewCount = effective.Count(g => g.CanView == true),
					CreateCount = effective.Count(g => g.CanCreate == true),
					EditCount = effective.Count(g => g.CanEdit == true),
					DeleteCount = effective.Count(g => g.CanDelete == true),
				};
			}).ToList();
		}

		public async Task<RoleMenuPermissionsDto?> GetAsync(int? companyId, int roleId, CancellationToken cancellationToken)
		{
			var role = await UsableRoles(companyId).AsNoTracking().SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken);
			if (role == null) return null;

			var items = await mRepository.GetQueryable<ApplicationMenuItem>(i => i.MenuId == MenuService.MainMenuId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			var rows = await mRepository.GetQueryable<Role2MenuItem>(g => g.PermissionRoleId == roleId && (g.CompanyId == null || g.CompanyId == companyId))
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			var effective = RoleMenuPermissionRules.Effective(rows, companyId).ToDictionary(g => g.ApplicationMenuItemId);

			return new RoleMenuPermissionsDto
			{
				RoleId = role.Id,
				RoleName = role.Name,
				IsSystem = role.CompanyId == null,
				IsOverridden = companyId is int company && RoleMenuPermissionRules.HasCompanyRows(rows, company),
				Items = RoleMenuPermissionRules.BuildTree(items, effective),
			};
		}

		public async Task<SetRoleMenuPermissionsOutcome> SetAsync(int? companyId, int roleId, SetRoleMenuPermissionsDto request, CancellationToken cancellationToken)
		{
			if (!await UsableRoles(companyId).AnyAsync(r => r.Id == roleId, cancellationToken)) return SetRoleMenuPermissionsOutcome.RoleNotFound;

			var requested = request.Items ?? [];
			if (requested.Select(g => g.ApplicationMenuItemId).Distinct().Count() != requested.Count) return SetRoleMenuPermissionsOutcome.DuplicateMenuItem;
			var menuItemIds = await MenuItemIdsAsync(cancellationToken);
			var known = menuItemIds.ToHashSet();
			if (requested.Any(g => !known.Contains(g.ApplicationMenuItemId))) return SetRoleMenuPermissionsOutcome.UnknownMenuItem;

			var wanted = (companyId == null
					? RoleMenuPermissionRules.Normalize(requested)
					: RoleMenuPermissionRules.FullSet(menuItemIds, requested))
				.ToDictionary(g => g.ApplicationMenuItemId);

			// Only this scope's rows of this menu are touched. Everything is one SaveChanges, so a role is never left half changed.
			var existing = await mRepository.GetQueryable<Role2MenuItem>(g => g.PermissionRoleId == roleId && g.CompanyId == companyId && menuItemIds.Contains(g.ApplicationMenuItemId))
				.ToListAsync(cancellationToken);

			foreach (var row in existing)
			{
				if (wanted.Remove(row.ApplicationMenuItemId, out var grant))
				{
					row.CanView = grant.CanView;
					row.CanCreate = grant.CanCreate;
					row.CanEdit = grant.CanEdit;
					row.CanDelete = grant.CanDelete;
				}
				else
				{
					mRepository.Delete(row);
				}
			}

			foreach (var grant in wanted.Values)
			{
				mRepository.Create(new Role2MenuItem
				{
					PermissionRoleId = roleId,
					CompanyId = companyId,
					ApplicationMenuItemId = grant.ApplicationMenuItemId,
					CanView = grant.CanView,
					CanCreate = grant.CanCreate,
					CanEdit = grant.CanEdit,
					CanDelete = grant.CanDelete,
				});
			}

			await mRepository.SaveAsync(cancellationToken);
			return SetRoleMenuPermissionsOutcome.Updated;
		}

		public async Task<SetRoleMenuPermissionsOutcome> ResetAsync(int companyId, int roleId, CancellationToken cancellationToken)
		{
			if (!await UsableRoles(companyId).AnyAsync(r => r.Id == roleId, cancellationToken)) return SetRoleMenuPermissionsOutcome.RoleNotFound;

			var rows = await mRepository.GetQueryable<Role2MenuItem>(g => g.PermissionRoleId == roleId && g.CompanyId == companyId).ToListAsync(cancellationToken);
			mRepository.DeleteAll(rows);
			await mRepository.SaveAsync(cancellationToken);
			return SetRoleMenuPermissionsOutcome.Updated;
		}

		/// <summary>The roles of the scope: the system roles for the defaults; those and the company's own for a company.</summary>
		private IQueryable<PermissionRole> UsableRoles(int? companyId) =>
			mRepository.GetQueryable<PermissionRole>(r => r.CompanyId == null || (companyId != null && r.CompanyId == companyId));

		private Task<List<int>> MenuItemIdsAsync(CancellationToken cancellationToken) =>
			mRepository.GetQueryable<ApplicationMenuItem>(i => i.MenuId == MenuService.MainMenuId)
				.AsNoTracking()
				.OrderBy(i => i.Id)
				.Select(i => i.Id)
				.ToListAsync(cancellationToken);

		private readonly IRepository mRepository;
	}
}
