using api_server.Controllers.Menu.Services;
using api_server.Controllers.Roles.Dto;
using api_server.core;
using db_model.AppStructure;
using db_model.Companies;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Roles.Services
{
	public enum SetRoleMenuPermissionsOutcome
	{
		Updated,
		/// <summary>No such role, or one that belongs to another company.</summary>
		RoleNotFound,
		/// <summary>Portal Admin always has everything.</summary>
		ReadOnlyRole,
		/// <summary>An entry names an item that is not in the menu.</summary>
		UnknownMenuItem,
		/// <summary>Two entries name the same item.</summary>
		DuplicateMenuItem,
	}

	public interface IRoleMenuPermissionsService
	{
		/// <summary>The roles the company can use (the system-wide ones and its own), with what each may do in the menu.</summary>
		Task<IReadOnlyList<RoleSummaryDto>> ListRolesAsync(int companyId, CancellationToken cancellationToken);

		/// <summary>The whole menu with the role's flags on it; null when the role does not exist or is another company's.</summary>
		Task<RoleMenuPermissionsDto?> GetAsync(int companyId, int roleId, CancellationToken cancellationToken);

		/// <summary>Replaces the role's flags on the menu: what the request lists is stored, everything else is taken away.</summary>
		Task<SetRoleMenuPermissionsOutcome> SetAsync(int companyId, int roleId, SetRoleMenuPermissionsDto request, CancellationToken cancellationToken);
	}

	public sealed class RoleMenuPermissionsService : IRoleMenuPermissionsService
	{
		public RoleMenuPermissionsService(IRepository repository)
		{
			mRepository = repository;
		}

		public async Task<IReadOnlyList<RoleSummaryDto>> ListRolesAsync(int companyId, CancellationToken cancellationToken)
		{
			var roles = await mRepository.GetQueryable<PermissionRole>(r => r.CompanyId == null || r.CompanyId == companyId)
				.AsNoTracking()
				.OrderBy(r => r.Id)
				.ToListAsync(cancellationToken);

			var menuItemIds = await MenuItemIdsAsync(cancellationToken);
			var grants = await mRepository.GetQueryable<Role2MenuItem>(g => menuItemIds.Contains(g.ApplicationMenuItemId))
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			var members = await mRepository.GetQueryable<User2Company>(m => m.CompanyId == companyId)
				.GroupBy(m => m.PermissionRoleId)
				.Select(g => new { RoleId = g.Key, Count = g.Count() })
				.ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

			return roles.Select(r =>
			{
				var own = grants.Where(g => g.PermissionRoleId == r.Id).ToList();
				return new RoleSummaryDto
				{
					Id = r.Id,
					Name = r.Name,
					Description = r.Description,
					IsSystem = r.CompanyId == null,
					IsReadOnly = IsReadOnly(r),
					MemberCount = members.GetValueOrDefault(r.Id),
					TotalMenus = menuItemIds.Count,
					ViewCount = own.Count(g => g.CanView == true),
					CreateCount = own.Count(g => g.CanCreate == true),
					EditCount = own.Count(g => g.CanEdit == true),
					DeleteCount = own.Count(g => g.CanDelete == true),
				};
			}).ToList();
		}

		public async Task<RoleMenuPermissionsDto?> GetAsync(int companyId, int roleId, CancellationToken cancellationToken)
		{
			var role = await FindUsableRoleAsync(companyId, roleId, cancellationToken);
			if (role == null) return null;

			var items = await mRepository.GetQueryable<ApplicationMenuItem>(i => i.MenuId == MenuService.MainMenuId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			var grants = await mRepository.GetQueryable<Role2MenuItem>(g => g.PermissionRoleId == roleId)
				.AsNoTracking()
				.ToDictionaryAsync(g => g.ApplicationMenuItemId, cancellationToken);

			return new RoleMenuPermissionsDto
			{
				RoleId = role.Id,
				RoleName = role.Name,
				IsReadOnly = IsReadOnly(role),
				Items = RoleMenuPermissionRules.BuildTree(items, grants),
			};
		}

		public async Task<SetRoleMenuPermissionsOutcome> SetAsync(int companyId, int roleId, SetRoleMenuPermissionsDto request, CancellationToken cancellationToken)
		{
			var role = await FindUsableRoleAsync(companyId, roleId, cancellationToken);
			if (role == null) return SetRoleMenuPermissionsOutcome.RoleNotFound;
			if (IsReadOnly(role)) return SetRoleMenuPermissionsOutcome.ReadOnlyRole;

			var requested = request.Items ?? [];
			if (requested.Select(g => g.ApplicationMenuItemId).Distinct().Count() != requested.Count) return SetRoleMenuPermissionsOutcome.DuplicateMenuItem;
			var menuItemIds = (await MenuItemIdsAsync(cancellationToken)).ToHashSet();
			if (requested.Any(g => !menuItemIds.Contains(g.ApplicationMenuItemId))) return SetRoleMenuPermissionsOutcome.UnknownMenuItem;

			var wanted = RoleMenuPermissionRules.Normalize(requested).ToDictionary(g => g.ApplicationMenuItemId);

			// Only this menu's rows are touched. Everything is one SaveChanges, so a role is never left half changed.
			var existing = await mRepository.GetQueryable<Role2MenuItem>(g => g.PermissionRoleId == roleId && menuItemIds.Contains(g.ApplicationMenuItemId))
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

		/// <summary>A role every company can use, or one of the company's own: the same rule as giving a user a role.</summary>
		private Task<PermissionRole?> FindUsableRoleAsync(int companyId, int roleId, CancellationToken cancellationToken) =>
			mRepository.GetQueryable<PermissionRole>(r => r.Id == roleId && (r.CompanyId == null || r.CompanyId == companyId))
				.AsNoTracking()
				.SingleOrDefaultAsync(cancellationToken);

		private Task<List<int>> MenuItemIdsAsync(CancellationToken cancellationToken) =>
			mRepository.GetQueryable<ApplicationMenuItem>(i => i.MenuId == MenuService.MainMenuId)
				.AsNoTracking()
				.Select(i => i.Id)
				.ToListAsync(cancellationToken);

		private static bool IsReadOnly(PermissionRole role) => role.CompanyId == null && role.Name == AuthPolicies.PortalAdminRole;

		private readonly IRepository mRepository;
	}
}
