using db_model.Notifications;
using exs.Database.Commons.Interfaces;
using System.Threading.Channels;

namespace api_server.Services.Notifications
{
	/// <summary>
	/// Transport-agnostic notification fan-out: buffers incoming requests in memory, then persists
	/// each as a Notification + one NotificationToUser per user + one NotificationQueue row per
	/// (user, transport). Transport-specific senders (e.g. the FCM sender) own draining
	/// NotificationQueue from there.
	/// </summary>
	public class NotificationService : BackgroundService, INotifier
	{
		public NotificationService(IServiceScopeFactory scopeFactory, ILogger<NotificationService> logger)
		{
			mScopeFactory = scopeFactory;
			mLogger = logger;
		}

		public async Task QueueNotificationAsync(Notification notification, IEnumerable<int> userIds, CancellationToken cancellationToken)
		{
			await mNotificationQueue.Writer.WriteAsync(new NotificationQueueItem
			{
				Notification = notification,
				UserIds = userIds.ToList()
			}, cancellationToken);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			// TryComplete, not Complete: the host can call StopAsync on a hosted service more than
			// once during shutdown (observed via WebApplicationFactory's teardown) - Complete()
			// throws ChannelClosedException on a channel that's already completed, TryComplete()
			// just returns false.
			mNotificationQueue.Writer.TryComplete();
			await base.StopAsync(cancellationToken);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await Task.Yield(); // Ensure this method is asynchronous
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					if (await mNotificationQueue.Reader.WaitToReadAsync(stoppingToken))
					{
						var items = new List<NotificationQueueItem>();
						while (mNotificationQueue.Reader.TryRead(out var item))
						{
							items.Add(item);
						}

						await processQueueItems(items, stoppingToken);
					}
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					return;
				}
				catch (Exception ex)
				{
					mLogger.LogError(ex, "Error processing notification queue");
				}
			}
		}

		private async Task processQueueItems(List<NotificationQueueItem> items, CancellationToken stoppingToken)
		{
			var attemptLeft = 5;
			while (attemptLeft > 0)
			{
				try
				{
					// A retried attempt reuses the same Notification instances: EF materializes generated
					// key values onto the entity as soon as it reads them back, even if a later statement
					// in the same SaveChanges batch then fails and rolls the transaction back - so a stale
					// Id from the previous, failed attempt must be cleared before it is Create()'d again.
					foreach (var item in items)
					{
						item.Notification.Id = 0;
					}

					using var scope = mScopeFactory.CreateScope();
					var db = scope.ServiceProvider.GetRequiredService<IRepository>();

					foreach (var item in items)
					{
						db.Create(item.Notification);

						foreach (var userId in item.UserIds.Distinct())
						{
							db.Create(new NotificationToUser
							{
								UserId = userId,
								Notification = item.Notification
							});

							db.Create(new NotificationQueue
							{
								UserId = userId,
								Notification = item.Notification,
								TransportType = NotificationTransportType.FCM,
								Created = DateTime.UtcNow
							});
						}
					}

					await db.SaveAsync(stoppingToken);
					return;
				}
				catch (Exception ex) when (!(ex is OperationCanceledException))
				{
					attemptLeft--;
					mLogger.LogError(ex, "Error processing notification queue, attempts left: {attemptsLeft}", attemptLeft);
					if (attemptLeft == 0)
					{
						throw;
					}
					await Task.Delay(FAIL_DELAY, stoppingToken); // Wait before retrying
				}
			}
		}

		private class NotificationQueueItem
		{
			public Notification Notification { get; set; } = null!;
			public List<int> UserIds { get; set; } = null!;
		}

		private readonly TimeSpan FAIL_DELAY = TimeSpan.FromSeconds(1);

		private readonly IServiceScopeFactory mScopeFactory;
		private readonly ILogger<NotificationService> mLogger;
		private readonly Channel<NotificationQueueItem> mNotificationQueue = Channel.CreateUnbounded<NotificationQueueItem>();
	}
}
