namespace db_model.Media
{
	public class UploadedMedia
	{
		public int Id { get; set; }
        public string? OriginalFileName { get; set; } = default!;
        public string FileName { get; set; } = default!;
        public string? PreviewFileName { get; set; }
		public byte MediaType { get; set; } //MediaType
        public string Guid { get; set; } = default!;

        public DateTime LatestUpdate { get; set; }
	}
}
