namespace db_model.AppStructure
{
	public class ApplicationMenu
	{
		public int Id { get; set; }
		public short MenuKind { get; set; }
		public string? Name { get; set; }
		public int? OwnerCompanyId { get; set; }
		public DateTime LatestUpdate { get; set; }
	}
}
