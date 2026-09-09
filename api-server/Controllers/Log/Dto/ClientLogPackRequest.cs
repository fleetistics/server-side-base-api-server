namespace api_server.Controllers.Log.Dto
{
	public class ClientLogPackRequest
	{
		public string UserDescription { get; set; } = string.Empty;
		public string IssueContext { get; set; } = string.Empty;
		public string? ClientSideInfo { get; set; }
		public string? ClientSideDate { get; set; }

		// Gzipped concatenation of the device's rotated log files; absent when the
		// client had no log files to send (see logUploadService.ts submitIssueReport).
		public IFormFile? File { get; set; }
	}
}
