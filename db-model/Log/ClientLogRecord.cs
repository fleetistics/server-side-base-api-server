namespace db_model.Log
{
	public class ClientLogRecord
	{
		public int Id { get; set; }
		//public DateTime Date { get; set; }
		public int UserId { get; set; }
		public string Log { get; set; } = string.Empty;
        public string? ClientSideInfo { get; set; }
        public string? UserDescription { get; set; }
        public string? IssueContext { get; set; }
        public string? ClientSideDate { get; set; }
        public DateTime LatestUpdate { get; set; }
	}
}
