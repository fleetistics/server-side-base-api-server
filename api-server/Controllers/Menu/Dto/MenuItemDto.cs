namespace api_server.Controllers.Menu.Dto
{
	/// <summary>
	/// One entry of the caller's menu (GET /api/menu): a page link, or a group that holds <see cref="Children"/>.
	/// The list is already limited to what the caller's role may view and sorted as it should be shown.
	/// </summary>
	public class MenuItemDto
	{
		public int Id { get; set; }

		public string Title { get; set; } = default!;

		public string? Description { get; set; }

		/// <summary>The icon's name in the portal's icon set, for example "Clipboard".</summary>
		public string? Icon { get; set; }

		/// <summary>
		/// The portal route (for example "/jobs/schedule"). Null for a group that is not a page of its own: clicking it only
		/// expands it. A group with a path is also a page: clicking it navigates as well as expands.
		/// </summary>
		public string? Path { get; set; }

		public bool DividerBefore { get; set; }

		public bool DividerAfter { get; set; }

		/// <summary>What the caller's role may do on this item. The item is only in the list when the role may view it, or when a child is.</summary>
		public bool CanEdit { get; set; }

		public bool CanCreate { get; set; }

		public bool CanDelete { get; set; }

		/// <summary>The items below this one, in the order to show them; empty for a leaf.</summary>
		public List<MenuItemDto> Children { get; set; } = [];
	}
}
