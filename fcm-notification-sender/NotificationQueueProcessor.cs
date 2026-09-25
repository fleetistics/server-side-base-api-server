using db_model.AppStructure;
using db_model.Notifications;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using fcm_notification_sender.Fcm;
using Microsoft.EntityFrameworkCore;

namespace fcm_notification_sender
{
	/// <summary>
	/// Resolves one NotificationQueue row (TransportType = FCM) to the user's currently-active
	/// mobile sessions, sends to each (with in-memory retry via ResilientFcmSender), then writes the
	/// terminal NotificationSent + one FCMNotificationSent per session attempted, and removes the
	/// queue row. Scoped: owns its own IRepository/DbContext, one instance per queue row processed.
	/// </summary>
	public sealed class NotificationQueueProcessor
	{
		public NotificationQueueProcessor(IRepository repository, ResilientFcmSender sender, ILogger<NotificationQueueProcessor> logger)
		{
			mRepository = repository;
			mSender = sender;
			mLogger = logger;
		}

		public async Task ProcessAsync(NotificationQueue queueItem, CancellationToken cancellationToken)
		{
			if (queueItem.Notification == null)
			{
				throw new InvalidOperationException($"NotificationQueue #{queueItem.Id} has no loaded Notification (dangling NotificationId {queueItem.NotificationId}?).");
			}

			// Only UserId/StatusId are filtered in the query itself - both plain scalars on
			// UserSession. Platform/FCMToken live on the owned ClientInfo type, and matching on those
			// is left to LINQ-to-Objects below rather than folded into the same DB predicate: a user
			// has at most a handful of sessions, so filtering the rest in memory costs nothing here,
			// and it keeps this query from depending on a provider's ability to translate member
			// access through an owned type inside a boolean expression.
			var candidateSessions = await mRepository.GetQueryable<UserSession>(s =>
					s.UserId == queueItem.UserId &&
					s.StatusId == UserSessionStatus.Active)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var activeSessions = candidateSessions
				.Where(s =>
					(s.ClientInfo.ClientDevicePlatformId == ClientDevicePlatform.Ios || s.ClientInfo.ClientDevicePlatformId == ClientDevicePlatform.Android) &&
					s.ClientInfo.FCMToken != "")
				.ToList();

			var title = queueItem.Notification.Title ?? "";
			var body = queueItem.Notification.Body ?? "";
			var payload = queueItem.Notification.Payload ?? "";

			var attempts = new List<FCMNotificationSent>(activeSessions.Count);
			foreach (var session in activeSessions)
			{
				var (result, attemptCount) = await mSender.SendWithRetryAsync(
					session.ClientInfo.FCMToken, session.ClientInfo.ClientDevicePlatformId, title, body,
					queueItem.Notification.TypeId, queueItem.Notification.EntityId, payload, cancellationToken);

				if (result.Outcome != FcmSendOutcome.Ok)
				{
					mLogger.LogWarning(
						"FCM send failed for NotificationQueue #{QueueId}, session #{SessionId}, after {Attempts} attempt(s): {ErrorCode} {ErrorMessage}",
						queueItem.Id, session.Id, attemptCount, result.ErrorCode, result.ErrorMessage);
				}

				attempts.Add(new FCMNotificationSent
				{
					SessionId = session.Id,
					FCMToken = session.ClientInfo.FCMToken,
					AttemptNumber = (byte)attemptCount,
					Result = result.Outcome == FcmSendOutcome.Ok ? NotificationResult.Ok : NotificationResult.Error,
					ErrorCode = result.ErrorCode,
					ProviderMessageId = result.ProviderMessageId,
					SentAt = DateTime.UtcNow,
				});
			}

			// Ok as soon as at least one active session actually got the push; zero active sessions
			// (nobody to deliver to right now) counts the same as "every attempt failed".
			var overallResult = attempts.Any(a => a.Result == NotificationResult.Ok) ? NotificationResult.Ok : NotificationResult.Error;

			var sent = new NotificationSent
			{
				NotificationId = queueItem.NotificationId,
				TransportType = queueItem.TransportType,
				UserId = queueItem.UserId,
				Created = queueItem.Created,
				SentAt = DateTime.UtcNow,
				Result = overallResult,
			};
			mRepository.Create(sent);

			foreach (var attempt in attempts)
			{
				attempt.NotificationSent = sent; // nav fixup: EF resolves NotificationSentId once `sent` gets its Id below
				mRepository.Create(attempt);
			}

			// queueItem was loaded in a different scope/DbContext (the poller's) - delete by id
			// rather than passing the tracked-elsewhere instance, so there's nothing to re-attach.
			mRepository.Delete<NotificationQueue>(queueItem.Id);

			await mRepository.SaveAsync(cancellationToken);
		}

		private readonly IRepository mRepository;
		private readonly ResilientFcmSender mSender;
		private readonly ILogger<NotificationQueueProcessor> mLogger;
	}
}
