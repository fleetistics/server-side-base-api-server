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
