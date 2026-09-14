using System.Threading.Channels;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using db_model.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace api_server.Controllers.Media
{
	public interface IMediaPreviewQueue
	{
		/// <summary>
		/// Queues an already-saved UploadedMedia row for preview generation. Fire-and-forget:
		/// nothing here confirms the preview was made, and a missed/failed entry (process
		/// restart, transient error) is picked up later by MediaPreviewService's own
		/// startup backfill scan rather than being retried inline.
		/// </summary>
		void Enqueue(int mediaId);
	}

	/// <summary>
	/// Generates a proportionally-resized preview for image uploads, off the request path:
	/// a singleton background worker with an in-process channel as its work queue, so a
	/// slow or failing resize never blocks or fails the upload response. Runs the queue
	/// backlog through a single reader (SingleReader = true lets the channel skip
	/// internal locking on the consume side), and on startup — before draining the
	/// channel — backfills any Image row still missing a PreviewFileName, which covers
	/// both rows that predate this feature and any Enqueue missed by a crash/restart.
	/// </summary>
	public sealed class MediaPreviewService : BackgroundService, IMediaPreviewQueue
	{
		private readonly Channel<int> mQueue = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
		{
			SingleReader = true,
		});

		private readonly IServiceScopeFactory mScopeFactory;
		private readonly MediaSettings mSettings;
		private readonly ILogger<MediaPreviewService> mLogger;

		public MediaPreviewService(IServiceScopeFactory scopeFactory, IOptions<MediaSettings> settings, ILogger<MediaPreviewService> logger)
		{
			mScopeFactory = scopeFactory;
			mSettings = settings.Value;
			mLogger = logger;
		}

		public void Enqueue(int mediaId) => mQueue.Writer.TryWrite(mediaId);

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			try
			{
				await EnqueuePendingAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				return;
			}
			catch (Exception ex)
			{
				// A failed backfill scan (e.g. DB unreachable or not yet migrated at cold
				// start) must not take the whole host down — an unhandled exception here
				// would stop this BackgroundService's ExecuteAsync entirely. New uploads
				// still get queued via Enqueue() and processed below; only the catch-up of
				// pre-existing rows is skipped for this run.
				mLogger.LogWarning(ex, "Startup preview backfill scan failed");
			}

			await foreach (var mediaId in mQueue.Reader.ReadAllAsync(stoppingToken))
			{
				try
				{
					await ProcessAsync(mediaId, stoppingToken);
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					return;
				}
				catch (Exception ex)
				{
					// One bad/corrupt image must not take down the whole preview pipeline.
					mLogger.LogWarning(ex, "Failed to generate preview for media {MediaId}", mediaId);
				}
			}
		}

		private async Task EnqueuePendingAsync(CancellationToken ct)
		{
			using var scope = mScopeFactory.CreateScope();
			var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

			var pendingIds = await repository
				.GetQueryable<UploadedMedia>(m => m.MediaType == MediaType.Image && string.IsNullOrEmpty(m.PreviewFileName))
				.Select(m => m.Id)
				.ToListAsync(ct);

			foreach (var id in pendingIds)
			{
				mQueue.Writer.TryWrite(id);
			}
		}

		private async Task ProcessAsync(int mediaId, CancellationToken ct)
		{
			using var scope = mScopeFactory.CreateScope();
			var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

			var media = await repository.GetQueryable<UploadedMedia>(m => m.Id == mediaId && m.MediaType == MediaType.Image && !m.FileName.StartsWith("file://") && string.IsNullOrEmpty(m.PreviewFileName)).FirstOrDefaultAsync(ct);
			if (media == null )
			{
                media = await repository.GetQueryable<UploadedMedia>(m => m.Id == mediaId ).FirstOrDefaultAsync(ct);
                mLogger.LogWarning($"mediaId {mediaId} missed media?.MediaType == MediaType.Image {media?.MediaType == MediaType.Image} m.FileName.StartsWith(file://) {media?.FileName?.StartsWith("file://")} string.IsNullOrEmpty(m.PreviewFileName) {string.IsNullOrEmpty(media?.PreviewFileName)} media?.Id {media?.Id}");
                return;
			}

			var uploadFolder = mSettings.UploadFolder
				?? throw new InvalidOperationException("MediaSettings:UploadFolder is not configured.");
			var sourcePath = Path.Combine(uploadFolder, media.FileName);
			if (!File.Exists(sourcePath))
			{
				mLogger.LogWarning("Source file {SourcePath} missing for media {MediaId}; skipping preview", sourcePath, mediaId);
				return;
			}

			var previewFileName = $"{Path.GetFileNameWithoutExtension(media.FileName)}_preview.png";
			var previewPath = Path.Combine(uploadFolder, previewFileName);

			using (var image = await Image.LoadAsync(sourcePath, ct))
			{
				// ResizeMode.Max fits the image inside the box on its longer side and keeps
				// the aspect ratio — exactly "shrink to fit", never crops or stretches.
				image.Mutate(x => x.Resize(new ResizeOptions
				{
					Size = new Size(mSettings.PreviewMaxWidth, mSettings.PreviewMaxHeight),
					Mode = ResizeMode.Max,
				}));
				await image.SaveAsync(previewPath, ct);
			}

			media.PreviewFileName = previewFileName;
			await repository.SaveAsync(ct);
		}
	}
}
