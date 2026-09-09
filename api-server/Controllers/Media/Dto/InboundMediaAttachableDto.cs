namespace api_server.Controllers.Media.Dto
{
	public abstract class InboundMediaAttachableDto
	{
		public List<InboundUploadedMediaDto>? InsertMedias { get; set; }
		public List<int>? RemoveMediaIds { get; set; }
	}
}
