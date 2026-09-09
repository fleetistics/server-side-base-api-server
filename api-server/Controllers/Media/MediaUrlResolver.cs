using api_server.Controllers.Media.Dto;
using Microsoft.Extensions.Options;

namespace api_server.Controllers.Media
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

		private string Combine(string relativePath) =>
			new Uri(new Uri(mSettings.BaseUrl!.TrimEnd('/') + '/'), relativePath.TrimStart('/')).ToString();
	}
}
