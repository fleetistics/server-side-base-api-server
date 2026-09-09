using api_server.Controllers.Base;
using api_server.Controllers.Media.Dto;
using api_server.Idempotency;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Media
{
	public class MediaSettings
	{
		public string? UploadFolder { get; set; }
		public string? BaseUrl { get; set; }

		/// <summary>Lower-case extensions accepted by uploads. Config key: Media:AllowedExtensions.</summary>
		public string[] AllowedExtensions { get; set; } =
			["jpg", "jpeg", "png", "gif", "webp", "svg", "pdf", "mp3", "mp4", "txt", "csv"];

		/// <summary>Per-file upload cap enforced by MediaStorageService. Config key: Media:MaxUploadBytes.</summary>
		public long MaxUploadBytes { get; set; } = 25 * 1024 * 1024;

		/// <summary>Preview image is scaled to fit inside this box, aspect ratio preserved. Config key: Media:PreviewMaxWidth.</summary>
		public int PreviewMaxWidth { get; set; } = 256;

		/// <summary>Config key: Media:PreviewMaxHeight.</summary>
		public int PreviewMaxHeight { get; set; } = 256;

		private HashSet<string>? mAllowedSet;
		public HashSet<string> AllowedExtensionsSet =>
			mAllowedSet ??= new HashSet<string>(AllowedExtensions, StringComparer.OrdinalIgnoreCase);
	}

	// Thin HTTP surface: validation, storage and persistence live in IMediaStorage
	// (MediaStorageService), where they are unit-testable and swappable.
	public class MediaController : AuthAPIController
	{
		public MediaController(IMediaStorage storage, MediaUrlResolver mediaUrls)
		{
			mStorage = storage;
			mMediaUrls = mediaUrls;
		}

		[HttpPost("/api/media/upload")]
		[Consumes("multipart/form-data")]
        [Idempotent]
        // Transport-level cap, slightly above MediaSettings.MaxUploadBytes so the
        // service can answer an oversized file with a clean 400 instead of Kestrel
        // cutting the connection.
        [RequestSizeLimit(32 * 1024 * 1024)]
		[ProducesResponseType(typeof(UploadedMediaDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		public async Task<IActionResult> Upload([FromForm] UploadRequest request, CancellationToken cancellationToken)
		{
			var result = await mStorage.SaveAsync(request, cancellationToken);
			if (result.Error is not null)
			{
				return BadRequest(new { message = result.Error });
			}

			var media = result.Media!;
			var dto = new UploadedMediaDto(media);
			mMediaUrls.ApplyAbsoluteUrl(dto);

			return Ok(dto);
		}

		private readonly IMediaStorage mStorage;
		private readonly MediaUrlResolver mMediaUrls;
	}
}
