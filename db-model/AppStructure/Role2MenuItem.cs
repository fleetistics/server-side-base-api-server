namespace db_model.AppStructure
{
	public class Role2MenuItem
	{
		public int Id { get; set; }

		public int PermissionRoleId { get; set; }
		public int ApplicationMenuItemId { get; set; }
		public bool? CanView { get; set; }
		public bool? CanEdit { get; set; }
		public bool? CanDelete { get; set; }
		public bool? CanCreate { get; set; }

		/// <summary>
		/// Null: the default permissions of a system role (PermissionRole.CompanyId null), which every company without a row of its own
		/// for that role uses. A company id: the rows that company uses for the role, either its own role or its override of a system role.
		/// </summary>
		public int? CompanyId { get; set; }

		public DateTime LatestUpdate { get; set; }
	}
}
