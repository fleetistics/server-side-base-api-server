using System.Text.RegularExpressions;
using api_server.Controllers.Media.Dto;
using api_server.core;
using exs.Database.Commons.Interfaces;
using db_model.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api_server.Controllers.Media
{
	/// <summary>Outcome of a media save: exactly one of Media / Error is set.</summary>
	public sealed record MediaSaveResult(UploadedMedia? Media, string? Error)
	{
		public static MediaSaveResult Ok(UploadedMedia media) => new(media, null);
		public static MediaSaveResult Fail(string error) => new(null, error);
	}

	public interface IMediaStorage
	{
		/// <summary>Validates, stores the file on disk and creates the DB record.</summary>
		Task<MediaSaveResult> SaveAsync(UploadRequest request, CancellationToken cancellationToken);
	}

	/// <summary>
	/// All upload logic lives here rather than in the controller: validation rules,
	/// filename sanitization and disk layout are business decisions, and behind
	/// IMediaStorage they are unit-testable and swappable (e.g. blob storage later)
	/// without touching the HTTP surface.
	/// </summary>
	public sealed partial class MediaStorageService : IMediaStorage
	{
		private readonly MediaSettings mSettings;
		private readonly IRepository mRepository;
		private readonly IMediaPreviewQueue mPreviewQueue;

		public MediaStorageService(IOptions<MediaSettings> settings, IRepository repository, IMediaPreviewQueue previewQueue)
		{
			mSettings = settings.Value;
			mRepository = repository;
			mPreviewQueue = previewQueue;
		}

		public async Task<MediaSaveResult> SaveAsync(UploadRequest request, CancellationToken cancellationToken)
		{
			if (request.File.Length == 0)
			{
				return MediaSaveResult.Fail("Uploaded file is empty.");
			}
			if (request.File.Length > mSettings.MaxUploadBytes)
			{
				return MediaSaveResult.Fail($"File exceeds the {mSettings.MaxUploadBytes} byte limit.");
			}
            if (string.IsNullOrWhiteSpace(request.Guid))
            {
                return MediaSaveResult.Fail("Guid is required.");
            }

            var uploadFolder = mSettings.UploadFolder
				?? throw new InvalidOperationException("MediaSettings:UploadFolder is not configured.");
			Directory.CreateDirectory(uploadFolder);
			string ext = "txt";
            switch (request.MediaType)
			{
				case MediaType.Image:
                    ext = "png";
                    break;
				case MediaType.Sound:
                    ext = "mp3";
                    break;	
				case MediaType.Video:
                    ext = "mp4";
                    break;
				default:
                    ext = "txt";
                    break;

            }

            var fileName = $"{request.Guid}.{ext}";
            await using (var stream = new FileStream(Path.Combine(uploadFolder, fileName), FileMode.Create, FileAccess.Write))
            {
                await request.File.CopyToAsync(stream, cancellationToken);
            }

            var media = await UploadedMediaTableLock.ExecuteAsync(mRepository.DbContext, async ct =>
			{
                var media = await mRepository.GetQueryable<UploadedMedia>(m => m.Guid == request.Guid).FirstOrDefaultAsync(ct);
				if (media == null)
				{
					media = new UploadedMedia
					{
						FileName = fileName,
						Guid = request.Guid,
						MediaType = request.MediaType,
						LatestUpdate = DateTime.UtcNow,
					};
					mRepository.Create(media);
				}
				else
				{
                    media.FileName = fileName;
					media.PreviewFileName = null;
                    media.MediaType = request.MediaType;
                    media.LatestUpdate = DateTime.UtcNow;
                }
                await mRepository.SaveAsync(ct);
                
                return media;

            }, cancellationToken);
            Telemetry.MediaUploadBytes.Record(request.File.Length);
            if (media.MediaType == MediaType.Image)
            {
                mPreviewQueue.Enqueue(media.Id);
            }

            return MediaSaveResult.Ok(media);
        }

	}
}
