namespace api_server.Controllers.Media.Dto
{
	public class UploadRequest
	{
		public IFormFile File { get; set; } = default!;
		public string? OriginalFileName { get; set; } = default!;
        public byte MediaType { get; set; }

		// Client-assigned identity for the logical media slot: re-uploading with the same
		// Guid replaces the file on the existing UploadedMedia row instead of creating a new one.
		public string Guid { get; set; } = default!;
	}
}
