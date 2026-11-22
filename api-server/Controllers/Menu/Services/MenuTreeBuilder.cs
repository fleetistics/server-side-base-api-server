using api_server.Controllers.Menu.Dto;
using db_model.AppStructure;

namespace api_server.Controllers.Menu.Services
{
	/// <summary>Turns the rows of one menu and the permissions of one role into the tree the caller sees. No database access.</summary>
	public static class MenuTreeBuilder
	{
		/// <summary>
		/// An item is in the tree when the role may view it (a <see cref="Role2MenuItem"/> row with CanView), or when something
		/// below it is: a group without a row of its own stays as the way to reach the item below it, without any rights
		/// (CanEdit, CanCreate and CanDelete false). A role with no row for an item does not see it. Siblings are in OrderBy order
		/// (items without one last), then by Id.
		/// </summary>
		public static List<MenuItemDto> Build(IReadOnlyCollection<ApplicationMenuItem> items, IReadOnlyCollection<Role2MenuItem> grants)
		{
			var byId = items.ToDictionary(i => i.Id);

			var granted = new Dictionary<int, Role2MenuItem>();
			foreach (var grant in grants)
			{
				if (grant.CanView == true && byId.ContainsKey(grant.ApplicationMenuItemId)) granted[grant.ApplicationMenuItemId] = grant;
			}

			// The granted items and everything above them. Stops at an id already added, which also ends a (malformed) cycle.
			var shown = new HashSet<int>();
			foreach (var id in granted.Keys)
			{
				var current = id;
				while (shown.Add(current))
				{
					if (byId[current].ParentId is not int parentId || !byId.ContainsKey(parentId)) break;
					current = parentId;
				}
			}

			var dtos = new Dictionary<int, MenuItemDto>();
			foreach (var id in shown)
			{
				var item = byId[id];
				granted.TryGetValue(id, out var grant);
				dtos[id] = new MenuItemDto
				{
					Id = item.Id,
					Title = item.Title,
					Description = item.Description,
					Icon = item.Icon,
					Path = item.Path,
					DividerBefore = item.DividerBefore ?? false,
					DividerAfter = item.DividerAfter ?? false,
					CanEdit = grant?.CanEdit ?? false,
					CanCreate = grant?.CanCreate ?? false,
					CanDelete = grant?.CanDelete ?? false,
				};
			}

			var roots = new List<MenuItemDto>();
			foreach (var item in shown.Select(id => byId[id]).OrderBy(i => i.OrderBy ?? short.MaxValue).ThenBy(i => i.Id))
			{
				var dto = dtos[item.Id];
				if (item.ParentId is int parentId && dtos.TryGetValue(parentId, out var parent)) parent.Children.Add(dto);
				else roots.Add(dto);
			}

			return roots;
		}
	}
}
