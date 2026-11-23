using api_server.Controllers.Roles.Dto;
using db_model.AppStructure;

namespace api_server.Controllers.Roles.Services
{
	/// <summary>The rules for a role's menu permissions that need no database: what a request means, and the tree a screen shows.</summary>
	public static class RoleMenuPermissionRules
	{
		/// <summary>
		/// What is stored for a request: ticking create, edit or delete implies view, and an entry with nothing ticked is no entry
		/// (a role without a row for an item does not see it). Create, edit and delete are only remembered for now; nothing enforces them.
		/// </summary>
		public static List<RoleMenuItemGrantDto> Normalize(IEnumerable<RoleMenuItemGrantDto> grants) =>
			grants
				.Select(g => new RoleMenuItemGrantDto
				{
					ApplicationMenuItemId = g.ApplicationMenuItemId,
					CanView = g.CanView || g.CanCreate || g.CanEdit || g.CanDelete,
					CanCreate = g.CanCreate,
					CanEdit = g.CanEdit,
					CanDelete = g.CanDelete,
				})
				.Where(g => g.CanView)
				.ToList();

		/// <summary>
		/// The rows that apply to a role for a company. The company's own rows when it has any for the role (they replace the defaults
		/// as a whole: an override lists every item, with false for what it does not allow), otherwise the defaults (CompanyId null).
		/// With no company only the defaults count. <paramref name="rows"/> may hold rows of other companies; they are ignored.
		/// </summary>
		public static List<Role2MenuItem> Effective(IEnumerable<Role2MenuItem> rows, int? companyId)
		{
			var list = rows.ToList();
			if (companyId is int company)
			{
				var own = list.Where(r => r.CompanyId == company).ToList();
				if (own.Count > 0) return own;
			}
			return list.Where(r => r.CompanyId == null).ToList();
		}

		/// <summary>True when the company has rows of its own for the role, i.e. it overrides the defaults or the role is its own.</summary>
		public static bool HasCompanyRows(IEnumerable<Role2MenuItem> rows, int companyId) => rows.Any(r => r.CompanyId == companyId);

		/// <summary>
		/// What a company override stores: every menu item, in the order given, with the flags of the request (normalized) or all false.
		/// Listing the items that are not allowed too is what tells an override that allows nothing from "no override".
		/// </summary>
		public static List<RoleMenuItemGrantDto> FullSet(IEnumerable<int> menuItemIds, IEnumerable<RoleMenuItemGrantDto> requested)
		{
			var wanted = Normalize(requested).ToDictionary(g => g.ApplicationMenuItemId);
			return menuItemIds
				.Select(id => wanted.TryGetValue(id, out var grant) ? grant : new RoleMenuItemGrantDto { ApplicationMenuItemId = id })
				.ToList();
		}

		/// <summary>
		/// The whole menu as a tree with the role's flags on every item (false where it has no row), siblings in OrderBy order
		/// (items without one last), then by Id. An item whose parent is not in the menu is a top-level one.
		/// </summary>
		public static List<RoleMenuItemPermissionDto> BuildTree(IReadOnlyCollection<ApplicationMenuItem> items, IReadOnlyDictionary<int, Role2MenuItem> grants)
		{
			var dtos = items.ToDictionary(i => i.Id, i =>
			{
				grants.TryGetValue(i.Id, out var grant);
				return new RoleMenuItemPermissionDto
				{
					Id = i.Id,
					Title = i.Title,
					Icon = i.Icon,
					Path = i.Path,
					CanView = grant?.CanView ?? false,
					CanCreate = grant?.CanCreate ?? false,
					CanEdit = grant?.CanEdit ?? false,
					CanDelete = grant?.CanDelete ?? false,
				};
			});

			var roots = new List<RoleMenuItemPermissionDto>();
			foreach (var item in items.OrderBy(i => i.OrderBy ?? short.MaxValue).ThenBy(i => i.Id))
			{
				var dto = dtos[item.Id];
				if (item.ParentId is int parentId && dtos.TryGetValue(parentId, out var parent)) parent.Children.Add(dto);
				else roots.Add(dto);
			}

			return roots;
		}
	}
}
