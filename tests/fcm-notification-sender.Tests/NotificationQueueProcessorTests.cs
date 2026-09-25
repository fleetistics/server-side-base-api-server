using db_model.AppStructure;
using db_model.Notifications;
using db_model.UserManagement;
using exs.Database.Commons.Impl;
using exs.Database.Commons.Interfaces;
using fcm_notification_sender.Fcm;
using mf.aiApi.mainDatabase.DatabaseContext;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace fcm_notification_sender.Tests
{
	/// <summary>
	/// Backs NotificationQueueProcessor with a real MainDatabaseContext (SQLite, in-memory) rather
	/// than a mocked IRepository - the processor's behavior is inseparable from real EF
	/// navigation-fixup (NotificationSent -> FCMNotificationSent) and query filtering, which a mock
	/// would either have to reimplement badly or not exercise at all. SQLite over EF's InMemory
	/// provider: InMemory's query translator/shaper choked on UserSession's owned ClientInfo type in
	/// this EF Core version, where SQLite (a real relational provider, closer to production Npgsql)
	/// handles it correctly. One open SqliteConnection per test keeps its in-memory DB alive across
	/// the multiple MainDatabaseContext instances used - mirroring the real Worker's "poller scope
	/// loads, processor scope writes" split.
	/// </summary>
	public sealed class NotificationQueueProcessorTests : IAsyncLifetime
	{
		private readonly SqliteConnection mConnection = new("Filename=:memory:");

		public async ValueTask InitializeAsync()
		{
			await mConnection.OpenAsync();
			// LatestUpdate columns are DEFAULT now() (a Postgres function, per
			// MainDatabaseContext.OnModelCreating) - SQLite has no such builtin, so register one
			// rather than working around every table that carries a LatestUpdate column.
			mConnection.CreateFunction("now", () => DateTime.UtcNow);
			await using var context = createContext();
			await context.Database.EnsureCreatedAsync();
		}

		public async ValueTask DisposeAsync() => await mConnection.DisposeAsync();

		[Fact]
		public async Task ProcessAsync_NotificationNavigationNotLoaded_Throws()
		{
			var processor = createProcessor(new StubFcmMessageSender(FcmSendResult.Ok("unused")));
			var queueItem = new NotificationQueue
			{
				Id = 1,
				NotificationId = 1,
				UserId = 1,
				TransportType = NotificationTransportType.FCM,
				Created = DateTime.UtcNow,
				Notification = null, // as if the caller forgot .Include(q => q.Notification)
			};

			await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(queueItem, CancellationToken.None));
		}

		[Fact]
		public async Task ProcessAsync_NoActiveSessions_ResultIsErrorAndQueueRowIsRemoved()
		{
			var queueItem = await seedNotificationAndQueueItemAsync(userId: 1);
			var stub = new StubFcmMessageSender(FcmSendResult.Ok("unused"));

			await createProcessor(stub).ProcessAsync(queueItem, CancellationToken.None);

			stub.Calls.ShouldBeEmpty(); // nobody to send to
			await using var db = createContext();
			(await db.Set<NotificationSent>().SingleAsync()).Result.ShouldBe(NotificationResult.Error);
			(await db.Set<FCMNotificationSent>().AnyAsync()).ShouldBeFalse();
			(await db.Set<NotificationQueue>().AnyAsync()).ShouldBeFalse();
		}

		[Fact]
		public async Task ProcessAsync_OneActiveSessionSucceeds_ResultIsOk()
		{
			var queueItem = await seedNotificationAndQueueItemAsync(userId: 1);
			await seedUserSessionAsync(userId: 1, UserSessionStatus.Active, ClientDevicePlatform.Ios, "token-1");
			var stub = new StubFcmMessageSender(FcmSendResult.Ok("msg-1"));

			await createProcessor(stub).ProcessAsync(queueItem, CancellationToken.None);

			stub.Calls.Count.ShouldBe(1);
			stub.Calls[0].Token.ShouldBe("token-1");
			await using var db = createContext();
			(await db.Set<NotificationSent>().SingleAsync()).Result.ShouldBe(NotificationResult.Ok);
			var attempt = await db.Set<FCMNotificationSent>().SingleAsync();
			attempt.Result.ShouldBe(NotificationResult.Ok);
			attempt.SessionId.ShouldBeGreaterThan(0);
			attempt.ProviderMessageId.ShouldBe("msg-1");
		}

		[Fact]
		public async Task ProcessAsync_OneSessionSucceedsOneFails_OverallResultIsOk()
		{
			var queueItem = await seedNotificationAndQueueItemAsync(userId: 1);
			await seedUserSessionAsync(userId: 1, UserSessionStatus.Active, ClientDevicePlatform.Ios, "token-1");
			await seedUserSessionAsync(userId: 1, UserSessionStatus.Active, ClientDevicePlatform.Android, "token-2");
			var stub = new StubFcmMessageSender(
				FcmSendResult.Ok("msg-1"),
				FcmSendResult.Failed(FcmSendOutcome.PermanentError, "UNREGISTERED", "dead"));

			await createProcessor(stub).ProcessAsync(queueItem, CancellationToken.None);

			stub.Calls.Count.ShouldBe(2);
			await using var db = createContext();
			(await db.Set<NotificationSent>().SingleAsync()).Result.ShouldBe(NotificationResult.Ok);
			var attempts = await db.Set<FCMNotificationSent>().ToListAsync();
			attempts.Count.ShouldBe(2);
			attempts.Count(a => a.Result == NotificationResult.Ok).ShouldBe(1);
			attempts.Count(a => a.Result == NotificationResult.Error).ShouldBe(1);
		}

		[Fact]
		public async Task ProcessAsync_AllSessionsFail_ResultIsError()
		{
			var queueItem = await seedNotificationAndQueueItemAsync(userId: 1);
			await seedUserSessionAsync(userId: 1, UserSessionStatus.Active, ClientDevicePlatform.Ios, "token-1");
			var stub = new StubFcmMessageSender(FcmSendResult.Failed(FcmSendOutcome.PermanentError, "UNREGISTERED", "dead"));

			await createProcessor(stub).ProcessAsync(queueItem, CancellationToken.None);

			await using var db = createContext();
			(await db.Set<NotificationSent>().SingleAsync()).Result.ShouldBe(NotificationResult.Error);
		}

		[Fact]
		public async Task ProcessAsync_OnlySendsToActiveMobileSessionsWithATokenForThatUser()
		{
			var queueItem = await seedNotificationAndQueueItemAsync(userId: 1);
			await seedUserSessionAsync(userId: 1, UserSessionStatus.Active, ClientDevicePlatform.Ios, "good-token"); // included
			await seedUserSessionAsync(userId: 1, UserSessionStatus.LoggedOutByUser, ClientDevicePlatform.Ios, "logged-out-token"); // wrong status
			await seedUserSessionAsync(userId: 1, UserSessionStatus.Active, ClientDevicePlatform.Windows, "windows-token"); // wrong platform
			await seedUserSessionAsync(userId: 1, UserSessionStatus.Active, ClientDevicePlatform.Android, ""); // no token
			await seedUserSessionAsync(userId: 2, UserSessionStatus.Active, ClientDevicePlatform.Ios, "other-users-token"); // wrong user
			var stub = new StubFcmMessageSender(FcmSendResult.Ok("msg"));

			await createProcessor(stub).ProcessAsync(queueItem, CancellationToken.None);

			stub.Calls.Count.ShouldBe(1);
			stub.Calls[0].Token.ShouldBe("good-token");
		}

		[Fact]
		public async Task ProcessAsync_TransientFailureThenSuccess_RecordsTwoAttempts()
		{
			var queueItem = await seedNotificationAndQueueItemAsync(userId: 1);
			await seedUserSessionAsync(userId: 1, UserSessionStatus.Active, ClientDevicePlatform.Ios, "token-1");
			var stub = new StubFcmMessageSender(
				FcmSendResult.Failed(FcmSendOutcome.TransientError, "UNAVAILABLE", "temporary"),
				FcmSendResult.Ok("msg-1"));

			await createProcessor(stub, maxAttempts: 3).ProcessAsync(queueItem, CancellationToken.None);

			stub.Calls.Count.ShouldBe(2);
			await using var db = createContext();
			var attempt = await db.Set<FCMNotificationSent>().SingleAsync();
			attempt.Result.ShouldBe(NotificationResult.Ok);
			attempt.AttemptNumber.ShouldBe((byte)2);
		}

		private MainDatabaseContext createContext() =>
			new(new DbContextOptionsBuilder<MainDatabaseContext>().UseSqlite(mConnection, o => o.UseNetTopologySuite()).Options);

		private NotificationQueueProcessor createProcessor(IFcmMessageSender sender, int maxAttempts = 1)
		{
			IRepository repository = new EntityFrameworkRepository<MainDatabaseContext>(createContext());
			var resilientSender = new ResilientFcmSender(sender, Options.Create(new FcmSenderOptions
			{
				MaxAttemptsPerSession = maxAttempts,
				RetryBaseDelayMilliseconds = 1,
			}));
			return new NotificationQueueProcessor(repository, resilientSender, NullLogger<NotificationQueueProcessor>.Instance);
		}

		private async Task<NotificationQueue> seedNotificationAndQueueItemAsync(int userId, short typeId = 1, int? entityId = null, string payload = "")
		{
			await using var context = createContext();
			var notification = new Notification
			{
				Date = DateTime.UtcNow,
				TypeId = typeId,
				EntityId = entityId,
				Title = "Title",
				Body = "Body",
				Payload = payload,
			};
			context.Add(notification);
			await context.SaveChangesAsync();

			var queue = new NotificationQueue
			{
				NotificationId = notification.Id,
				UserId = userId,
				TransportType = NotificationTransportType.FCM,
				Created = DateTime.UtcNow,
			};
			context.Add(queue);
			await context.SaveChangesAsync();

			// Reload detached with Notification populated - exactly what the real Worker's poller
			// query (.Include(q => q.Notification)) hands to a processor running in another scope.
			return await context.Set<NotificationQueue>()
				.Include(q => q.Notification)
				.AsNoTracking()
				.SingleAsync(q => q.Id == queue.Id);
		}

		private async Task seedUserSessionAsync(int userId, short statusId, short platformId, string fcmToken)
		{
			await using var context = createContext();
			context.Add(new UserSession
			{
				UserId = userId,
				StatusId = statusId,
				ClientInfo = new SessionClientInfo
				{
					ClientDevicePlatformId = platformId,
					FCMToken = fcmToken,
				},
			});
			await context.SaveChangesAsync();
		}
	}
}
