namespace db_model.AppStructure
{
	public class Role2MenuItem
	{
		public int PermissionRoleId { get; set; }
		public int ApplicationMenuItemId { get; set; }
		public bool? CanView { get; set; }
		public bool? CanEdit { get; set; }
		public bool? CanDelete { get; set; }
		public bool? CanCreate { get; set; }

		public DateTime LatestUpdate { get; set; }
	}
}
