namespace api_server.Medias
{
	public abstract class InboundMediaAttachableDto
	{
		public List<InboundUploadedMediaDto>? InsertMedias { get; set; }
		public List<int>? RemoveMediaIds { get; set; }
	}
}
