namespace db_model.AppStructure
{
	public class ApplicationMenuItem
	{
		public int Id { get; set; }
		public int MenuId { get; set; }
		public string Title { get; set; } = default!;
		public string? Description { get; set; }
		public string? Icon { get; set; }
		public int? ParentId { get; set; }
		public bool? DividerBefore { get; set; }
		public bool? DividerAfter { get; set; }
		public short? StatusId { get; set; }
		public short? OrderBy { get; set; }
		public string? Path { get; set; }
		public int? ApplicationPageId { get; set; }

		public DateTime LatestUpdate { get; set; }
	}
}
