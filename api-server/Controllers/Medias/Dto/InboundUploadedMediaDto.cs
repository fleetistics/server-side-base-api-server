namespace api_server.Medias
{
	public class InboundUploadedMediaDto
	{
        public string Url { get; set; } = default!;
        public string Guid { get; set; } = default!;
        public short? GroupKey { get; set; }
		public byte MediaType { get; set; } //MediaType
	}
}
