namespace api_server.Controllers.Roles.Dto
{
	/// <summary>A role as the Roles screen lists it, with what it may do in the menu.</summary>
	public class RoleSummaryDto
	{
		public int Id { get; set; }

		public string Name { get; set; } = default!;

		public string? Description { get; set; }

		/// <summary>True for a role every company can use; false for one that belongs to the caller's company.</summary>
		public bool IsSystem { get; set; }

		/// <summary>True when the role cannot be edited here: Portal Admin always has everything, so nobody can lock themselves out.</summary>
		public bool IsReadOnly { get; set; }

		/// <summary>Users who hold the role in the caller's company.</summary>
		public int MemberCount { get; set; }

		/// <summary>The menu items there are to give.</summary>
		public int TotalMenus { get; set; }

		/// <summary>How many menu items the role may view, create in, edit and delete in.</summary>
		public int ViewCount { get; set; }

		public int CreateCount { get; set; }

		public int EditCount { get; set; }

		public int DeleteCount { get; set; }
	}

	/// <summary>The whole menu as a tree, each item with the flags this role has on it (all false for an item it has no row for).</summary>
	public class RoleMenuPermissionsDto
	{
		public int RoleId { get; set; }

		public string RoleName { get; set; } = default!;

		/// <summary>See <see cref="RoleSummaryDto.IsReadOnly"/>: a PUT is refused.</summary>
		public bool IsReadOnly { get; set; }

		public List<RoleMenuItemPermissionDto> Items { get; set; } = [];
	}

	public class RoleMenuItemPermissionDto
	{
		public int Id { get; set; }

		public string Title { get; set; } = default!;

		public string? Icon { get; set; }

		/// <summary>The portal route; null for a group that only expands.</summary>
		public string? Path { get; set; }

		public bool CanView { get; set; }

		public bool CanCreate { get; set; }

		public bool CanEdit { get; set; }

		public bool CanDelete { get; set; }

		public List<RoleMenuItemPermissionDto> Children { get; set; } = [];
	}

	/// <summary>Body of PUT /api/admin/roles/{roleId}/menu-permissions: the role's flags, replacing what it had.</summary>
	public class SetRoleMenuPermissionsDto
	{
		/// <summary>One entry per menu item the role may do anything with. An item that is left out is not allowed at all.</summary>
		public List<RoleMenuItemGrantDto> Items { get; set; } = [];
	}

	public class RoleMenuItemGrantDto
	{
		public int ApplicationMenuItemId { get; set; }

		/// <summary>Create, edit and delete include view: whatever is ticked, the item is viewable.</summary>
		public bool CanView { get; set; }

		public bool CanCreate { get; set; }

		public bool CanEdit { get; set; }

		public bool CanDelete { get; set; }
	}
}
