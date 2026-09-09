using db_model.Media;

namespace api_server.Controllers.Media.Dto
{
	public class UploadedMediaDto
	{
		public UploadedMediaDto() { }
		public UploadedMediaDto(UploadedMedia media)
		{
			Id = media.Id;
			Url = media.FileName;
			PreviewUrl = media.PreviewFileName;
			MediaType = media.MediaType;
		}

		public int Id { get; set; }
		public string Url { get; set; }
        public string Guid { get; set; }
        public short? GroupKey { get; set; }
        public string? PreviewUrl { get; set; }
		public byte MediaType { get; set; } //MediaType
	}
}
