using api_server.Medias;
using Microsoft.Extensions.Options;

namespace api_server.Medias
{
	/// <summary>
	/// Turns stored media filenames into absolute URLs (Media:BaseUrl + filename).
	/// Single home for the rule — used by every endpoint that returns media DTOs,
	/// so all consumers get identical, immediately-usable URLs.
	/// </summary>
	public sealed class MediaUrlResolver
	{
		private readonly MediaSettings mSettings;

		public MediaUrlResolver(IOptions<MediaSettings> settings)
		{
			mSettings = settings.Value;
		}
        public void ApplyAbsoluteUrls(List<UploadedMediaDto>? medias)
        {
            if (medias == null)
            {
                return;
            }
			foreach (var media in medias) ApplyAbsoluteUrl(media);
        }
        public void ApplyAbsoluteUrl(UploadedMediaDto? media)
		{
			if (media == null)
			{
				return;
			}
			if (!string.IsNullOrEmpty(media.Url) &&!media.Url.StartsWith("file://")) media.Url = Combine(media.Url);
			media.PreviewUrl = string.IsNullOrEmpty(media.PreviewUrl) ? null : Combine(media.PreviewUrl);
		}
        public string? ApplyAbsoluteUrl(string? fileName)
        {
            if (!string.IsNullOrWhiteSpace(fileName) && !fileName.StartsWith("file://")) return Combine(fileName);
            return fileName;
        }

        private string Combine(string relativePath) =>
			new Uri(new Uri(mSettings.BaseUrl!.TrimEnd('/') + '/'), relativePath.TrimStart('/')).ToString();
	}
}
