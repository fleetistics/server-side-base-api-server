using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using exs.modelCommons.AppStructure;
using exs.modelCommons.UserManagement;
using exs.notifications_contracts;
using exs.notifications_model.Notifications;
using exs.notifications_model.Outbox;
using exs.notifications_model.Transports;
using exs.notifications_service.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// What the api-server does with notifications in the default (legacy queue) pipeline.
/// INotificationSender is the way a feature raises one: it adds the Notification to the feature's own
/// repository, so it commits with the feature's data, and hands the fan-out to the background
/// NotificationService when the scope ends. These tests poll the database for the expected state rather
/// than asserting immediately, because the fan-out runs on that background service.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class NotificationServiceTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;

    public NotificationServiceTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateMobileNotification_PersistsItAndFansOutOneQueueRowPerUser()
    {
        var userIds = new[] { 1001, 1002 };

        await NotificationRaiser.RaiseMobileAsync(_fixture, userIds, typeId: 7, entityId: 123, payload: """{"foo":"bar"}""");

        await NotificationRaiser.WaitUntilAsync(async () => await _fixture.QueryDbAsync(db => db.Set<FcmQueue>().CountAsync()) == 2);

        var savedNotification = await _fixture.QueryDbAsync(db => db.Set<Notification>().AsNoTracking().SingleAsync());
        savedNotification.TypeId.ShouldBe((short)7);
        savedNotification.EntityId.ShouldBe(123);
        savedNotification.Title.ShouldBe("Title");
        savedNotification.Body.ShouldBe("Body");
        savedNotification.Payload.ShouldBe("""{"foo":"bar"}""");
        savedNotification.TransportType.ShouldBe(NotificationTransportType.FCM);

        var toUserRows = await _fixture.QueryDbAsync(db => db.Set<NotificationToUser>().AsNoTracking().ToListAsync());
        toUserRows.Select(r => r.UserId).OrderBy(id => id).ShouldBe(userIds.OrderBy(id => id));
        toUserRows.ShouldAllBe(r => r.NotificationId == savedNotification.Id);

        var queueRows = await _fixture.QueryDbAsync(db => db.Set<FcmQueue>().AsNoTracking().ToListAsync());
        queueRows.Select(r => r.UserId).OrderBy(id => id).ShouldBe(userIds.OrderBy(id => id));
        queueRows.ShouldAllBe(r => r.NotificationId == savedNotification.Id);

        // Nothing of the sender-mode pipeline runs unless the host asks for it.
        (await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task CreateMobileNotification_DuplicateUserIdsInRequest_FansOutOnlyOncePerUser()
    {
        await NotificationRaiser.RaiseMobileAsync(_fixture, [2001, 2001, 2001], typeId: 1);

        // The whole batch (queue rows and recipients) is written in one SaveChangesAsync, so as soon as
        // any queue row exists the fan-out for this call is complete.
        await NotificationRaiser.WaitUntilAsync(async () => await _fixture.QueryDbAsync(db => db.Set<FcmQueue>().AnyAsync()));

        var queueRows = await _fixture.QueryDbAsync(db => db.Set<FcmQueue>().AsNoTracking().ToListAsync());
        queueRows.ShouldHaveSingleItem();
        queueRows[0].UserId.ShouldBe(2001);

        (await _fixture.QueryDbAsync(db => db.Set<NotificationToUser>().AsNoTracking().ToListAsync())).ShouldHaveSingleItem();
    }
}

/// <summary>
/// The host switched to NotificationService:FcmTransport = "Sender": the fan-out writes outbox rows
/// instead of queue rows, the relay submits them over HTTP to the sender (a local fake speaking the wire
/// contract), and the result puller records what comes back and clears dead tokens from user_session.
/// Real host, real PostgreSQL, real HTTP; only the sender app is a stand-in.
/// </summary>
[Collection(FcmModeApiCollection.Name)]
public sealed class FcmModeNotificationTests : IAsyncLifetime
{
    private readonly FcmModeApiFixture _fixture;

    public FcmModeNotificationTests(FcmModeApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync()
    {
        _fixture.Sender.Reset();
        return new(_fixture.ResetDatabaseAsync());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task EveryActiveDeviceOfEveryRecipientIsSent_AndAUserWithoutOneIsRecordedAsNoDestination()
    {
        var androidSession = await addSessionAsync(userId: 1, "token-android", ClientDevicePlatform.Android, FcmModeApiFixture.AccountedAppUid);
        var iosSession = await addSessionAsync(userId: 1, "token-ios", ClientDevicePlatform.Ios);
        await addSessionAsync(userId: 1, "token-closed", ClientDevicePlatform.Android, status: UserSessionStatus.LoggedOutByUser);

        await NotificationRaiser.RaiseMobileAsync(_fixture, [1, 2], typeId: 5, entityId: 77, title: "Hello", body: "World");

        await waitForRowsAsync(r => r.State == OutboxState.Completed || r.ErrorKind == OutboxErrorKind.NoDestination, expected: 3);

        var rows = await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().AsNoTracking().OrderBy(r => r.Id).ToListAsync());
        rows.Count(r => r.UserId == 1 && r.State == OutboxState.Completed && r.ResultStatus == "sent").ShouldBe(2);
        var withoutDevice = rows.Single(r => r.UserId == 2);
        withoutDevice.ErrorKind.ShouldBe(OutboxErrorKind.NoDestination);
        withoutDevice.State.ShouldBe(OutboxState.Completed);
        withoutDevice.ResultStatus.ShouldBe("failed");

        // What the sender was actually asked to deliver: two devices, each with its own token, platform, and
        // the account the app maps to (only the app configured for one gets one), and a ref pointing back at the session.
        var sent = _fixture.Sender.Messages;
        sent.Count.ShouldBe(2);
        var toAndroid = sent.Single(m => m.Body.Destination.Token == "token-android");
        toAndroid.Body.Destination.Platform.ShouldBe("android");
        toAndroid.Account.ShouldBe(FcmModeApiFixture.AccountForThatApp);
        toAndroid.Ref.ShouldBe($"s:{androidSession}");
        toAndroid.Body.Title.ShouldBe("Hello");
        toAndroid.Body.Body.ShouldBe("World");
        var toIos = sent.Single(m => m.Body.Destination.Token == "token-ios");
        toIos.Body.Destination.Platform.ShouldBe("ios");
        toIos.Account.ShouldBeNull();
        toIos.Ref.ShouldBe($"s:{iosSession}");

        // Nothing went through the legacy queue.
        (await _fixture.QueryDbAsync(db => db.Set<FcmQueue>().CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task ATokenTheProviderReportsDead_IsClearedFromTheUsersSessions_AndOnlyThatToken()
    {
        await addSessionAsync(userId: 1, "token-dead", ClientDevicePlatform.Android);
        var healthy = await addSessionAsync(userId: 1, "token-good", ClientDevicePlatform.Ios);
        _fixture.Sender.DeadTokens.Add("token-dead");

        await NotificationRaiser.RaiseMobileAsync(_fixture, [1], typeId: 5);

        await waitForRowsAsync(r => r.State == OutboxState.Completed, expected: 2);

        var dead = await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().AsNoTracking().SingleAsync(r => r.DestinationInvalid));
        dead.ErrorCode.ShouldBe("Unregistered");

        await NotificationRaiser.WaitUntilAsync(async () =>
            !await _fixture.QueryDbAsync(db => db.Set<UserSession>().AnyAsync(s => s.ClientInfo.FCM_FID == "token-dead")));

        var sessions = await _fixture.QueryDbAsync(db => db.Set<UserSession>().AsNoTracking().Where(s => s.UserId == 1).ToListAsync());
        sessions.Count.ShouldBe(2);
        sessions.Single(s => s.Id == healthy).ClientInfo.FCM_FID.ShouldBe("token-good");
        // The login itself is still valid; only the device token is gone.
        sessions.ShouldAllBe(s => s.StatusId == UserSessionStatus.Active);

        // So the next push to this user goes to the one device that is left.
        _fixture.Sender.Messages.Count.ShouldBe(2);
        await NotificationRaiser.RaiseMobileAsync(_fixture, [1], typeId: 5);
        await waitForRowsAsync(r => r.State == OutboxState.Completed, expected: 3);
        _fixture.Sender.Messages.Count(m => m.Body.Destination.Token == "token-dead").ShouldBe(1);
        _fixture.Sender.Messages.Count(m => m.Body.Destination.Token == "token-good").ShouldBe(2);
    }

    [Fact]
    public async Task CreateMobileNotification_WritesPushOutboxRows_AndNoEmailRow()
    {
        await addSessionAsync(userId: 1, "token-android", ClientDevicePlatform.Android);

        await NotificationRaiser.RaiseMobileAsync(_fixture, [1], typeId: 5, title: "Hello", body: "World");

        await NotificationRaiser.WaitUntilAsync(async () =>
            await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().CountAsync(r => r.State == OutboxState.Completed)) == 1);

        var rows = await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().AsNoTracking().ToListAsync());
        var push = rows.ShouldHaveSingleItem();
        push.Channel.ShouldBe(Channels.Fcm);
        push.UserId.ShouldBe(1);
        push.NotificationId.ShouldNotBeNull();
        rows.Count(r => r.Channel == Channels.Email).ShouldBe(0);

        var notification = await _fixture.QueryDbAsync(db => db.Set<Notification>().AsNoTracking().SingleAsync());
        notification.TransportType.ShouldBe(NotificationTransportType.FCM);
        notification.Id.ShouldBe(push.NotificationId!.Value);
    }

    [Fact]
    public async Task CreateEmail_WhenTheEmailTransportIsOff_ThrowsAndWritesNothing()
    {
        using (var scope = _fixture.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();
            var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

            var thrown = Should.Throw<InvalidOperationException>(() =>
                sender.CreateEmail(repository, new EmailMessage { To = "sam@example.com", Subject = "Hello", Text = "World" }));
            thrown.Message.ShouldContain("EmailTransport");

            await repository.SaveAsync(CancellationToken.None);
        }

        (await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().CountAsync())).ShouldBe(0);
        (await _fixture.QueryDbAsync(db => db.Set<Notification>().CountAsync())).ShouldBe(0);
    }

    private async Task<int> addSessionAsync(int userId, string token, short platform, string appUid = "", short status = UserSessionStatus.Active)
    {
        return await _fixture.QueryDbAsync(async db =>
        {
            var session = new UserSession
            {
                UserId = userId,
                StatusId = status,
                ClientInfo = new SessionClientInfo { FCM_FID = token, PlatformId = platform, AppUID = appUid },
            };
            db.Set<UserSession>().Add(session);
            await db.SaveChangesAsync();
            return session.Id;
        });
    }

    private Task waitForRowsAsync(System.Linq.Expressions.Expression<Func<OutboxMessage, bool>> done, int expected) =>
        NotificationRaiser.WaitUntilAsync(async () =>
            await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().Where(done).CountAsync()) >= expected);
}

internal static class NotificationRaiser
{
    /// <summary>Raises a push notification the way a feature does: through INotificationSender, committed with the feature's own save.</summary>
    public static async Task RaiseMobileAsync(ApiFixture fixture, IEnumerable<int> userIds, short typeId, int entityId = 1,
        string title = "Title", string body = "Body", string payload = "{}")
    {
        using var scope = fixture.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

        sender.CreateMobileNotification(repository, typeId, entityId, title, body, payload, userIds);
        await repository.SaveAsync(CancellationToken.None);
        // Leaving the scope hands the saved notification to the fan-out.
    }

    /// <summary>Raises an email notification (one email per user, at the address the host resolves), the same way.</summary>
    public static async Task RaiseEmailAsync(ApiFixture fixture, IEnumerable<int> userIds, short typeId, int entityId = 1,
        string title = "Title", string body = "Body", string payload = "{}")
    {
        using var scope = fixture.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

        sender.CreateEmailNotification(repository, typeId, entityId, title, body, payload, userIds);
        await repository.SaveAsync(CancellationToken.None);
    }

    /// <summary>Sends a free-form email: only an outbox row is written, committed with the feature's own save.</summary>
    public static async Task RaiseFreeFormEmailAsync(ApiFixture fixture, EmailMessage message)
    {
        using var scope = fixture.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

        sender.CreateEmail(repository, message);
        await repository.SaveAsync(CancellationToken.None);
        // Leaving the scope wakes the email relay, so the row is submitted at once.
    }

    public static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("The notification pipeline did not reach the expected state within the timeout.");
    }
}

/// <summary>
/// The host with the email transport on: CreateEmailNotification becomes one email per user, using the address on
/// the user record, and CreateEmail one free-form email to one address; both are relayed over HTTP to the standalone
/// email sender (a local fake speaking the wire contract). Push stays on the legacy queue. Real host, real
/// PostgreSQL, real HTTP; only the email sender app is a stand-in.
/// </summary>
[Collection(EmailModeApiCollection.Name)]
public sealed class EmailModeNotificationTests : IAsyncLifetime
{
    private readonly EmailModeApiFixture _fixture;

    public EmailModeNotificationTests(EmailModeApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync()
    {
        _fixture.Sender.Reset();
        return new(_fixture.ResetDatabaseAsync());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task AnEmailedNotification_IsMailedToEachActiveUserWithAnAddress_AndTheRestAreRecordedAsNotMailed()
    {
        var sam = await addUserAsync("sam@example.com", fullName: "Sam Rivera");
        var inactive = await addUserAsync("gone@example.com", status: UserStatus.Inactive);
        var noAddress = await addUserAsync(null);
        var malformed = await addUserAsync("Sam <sam@example.com>");
        var several = await addUserAsync("a@example.com, b@example.com");

        await NotificationRaiser.RaiseEmailAsync(_fixture, [sam, inactive, noAddress, malformed, several], EmailModeApiFixture.EmailedType, title: "Hello", body: "World");

        await waitForRowsAsync(r => r.State == OutboxState.Completed, expected: 5);

        var rows = await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().AsNoTracking().Where(o => o.Channel == "email").ToListAsync());
        rows.Single(r => r.UserId == sam).ResultStatus.ShouldBe("sent");
        foreach (var user in new[] { inactive, noAddress, malformed, several })
        {
            var row = rows.Single(r => r.UserId == user);
            row.ErrorKind.ShouldBe(OutboxErrorKind.NoDestination);
            row.ErrorCode.ShouldBe(OutboxErrorCode.NoEmailAddress);
        }

        // What the email sender was actually asked to deliver: one mail, to the one good address.
        var mail = _fixture.Sender.Messages.ShouldHaveSingleItem();
        mail.Channel.ShouldBe("email");
        mail.Ref.ShouldBe($"u:{sam}");
        mail.Account.ShouldBeNull(); // this host names none, so the email sender's default for it applies
        mail.Body.To.Address.ShouldBe("sam@example.com");
        mail.Body.To.Name.ShouldBe("Sam Rivera");
        mail.Body.Subject.ShouldBe("Hello");
        mail.Body.Text.ShouldBe("World");
    }

    [Fact]
    public async Task CreateMobileNotification_IsNotMailed_EvenForAUserWithAnAddress()
    {
        var sam = await addUserAsync("sam@example.com");

        await NotificationRaiser.RaiseMobileAsync(_fixture, [sam], EmailModeApiFixture.EmailedType);
        // The push goes through the legacy queue; once it is there and the notification is done, nothing else is coming.
        await NotificationRaiser.WaitUntilAsync(async () =>
            await _fixture.QueryDbAsync(db => db.Set<Notification>().AnyAsync(n => n.Status == NotificationStatus.Processed)));

        (await _fixture.QueryDbAsync(db => db.Set<FcmQueue>().CountAsync())).ShouldBe(1);
        (await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().CountAsync())).ShouldBe(0);
        _fixture.Sender.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task AMailboxTheServerSaysDoesNotExist_IsRecordedOnTheDeliveryRow()
    {
        var ghost = await addUserAsync("ghost@example.com");
        _fixture.Sender.DeadAddresses.Add("ghost@example.com");

        await NotificationRaiser.RaiseEmailAsync(_fixture, [ghost], EmailModeApiFixture.EmailedType);

        await waitForRowsAsync(r => r.State == OutboxState.Completed, expected: 1);
        var row = await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().AsNoTracking().SingleAsync(o => o.Channel == "email"));
        row.DestinationInvalid.ShouldBeTrue();
        row.ErrorCode.ShouldBe("550 5.1.1");
        // This host has nowhere to flag a bad address, so the user record is untouched.
        (await _fixture.QueryDbAsync(db => db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == ghost))).Email.ShouldBe("ghost@example.com");
    }

    [Fact]
    public async Task CreateEmailNotification_GoesOutAsAnEmailOnly_AndWritesNoPushRow()
    {
        var sam = await addUserAsync("sam@example.com");

        await NotificationRaiser.RaiseEmailAsync(_fixture, [sam], EmailModeApiFixture.EmailedType, title: "Hello", body: "World");

        await waitForRowsAsync(r => r.State == OutboxState.Completed, expected: 1);
        await NotificationRaiser.WaitUntilAsync(async () =>
            await _fixture.QueryDbAsync(db => db.Set<Notification>().AnyAsync(n => n.Status == NotificationStatus.Processed)));

        // No push in either pipeline: not on the legacy queue, and no outbox row for any other channel.
        (await _fixture.QueryDbAsync(db => db.Set<FcmQueue>().CountAsync())).ShouldBe(0);
        (await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().CountAsync(o => o.Channel != Channels.Email))).ShouldBe(0);

        var notification = await _fixture.QueryDbAsync(db => db.Set<Notification>().AsNoTracking().SingleAsync());
        notification.TransportType.ShouldBe(NotificationTransportType.Email);
        (await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().SingleAsync())).UserId.ShouldBe(sam);
        _fixture.Sender.Messages.ShouldHaveSingleItem().Body.To.Address.ShouldBe("sam@example.com");
    }

    [Fact]
    public async Task CreateEmail_SendsTheFreeFormMessage_WritesNoNotification_AndClearsTheEnvelopeOnceAccepted()
    {
        await NotificationRaiser.RaiseFreeFormEmailAsync(_fixture, new EmailMessage
        {
            To = "reset@example.com",
            ToName = "Reset Person",
            Subject = "Reset your password",
            Text = "Open https://example.test/reset/abc",
            Ref = "reset:42",
        });

        await waitForRowsAsync(r => r.State == OutboxState.Completed, expected: 1);

        // What the email sender was asked to deliver, exactly as given.
        var mail = _fixture.Sender.Messages.ShouldHaveSingleItem();
        mail.Channel.ShouldBe("email");
        mail.Account.ShouldBeNull();
        mail.Ref.ShouldBe("reset:42");
        mail.Body.To.Address.ShouldBe("reset@example.com");
        mail.Body.To.Name.ShouldBe("Reset Person");
        mail.Body.Subject.ShouldBe("Reset your password");
        mail.Body.Text.ShouldBe("Open https://example.test/reset/abc");

        // No notification log: the address and the link are not kept anywhere but the outbox row.
        (await _fixture.QueryDbAsync(db => db.Set<Notification>().CountAsync())).ShouldBe(0);
        (await _fixture.QueryDbAsync(db => db.Set<NotificationToUser>().CountAsync())).ShouldBe(0);

        // The row names neither a notification nor a user, the sender's result is recorded on it, and the
        // envelope (address and text) is gone now that the sender has its own copy.
        var row = await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().AsNoTracking().SingleAsync());
        row.NotificationId.ShouldBeNull();
        row.UserId.ShouldBeNull();
        row.Channel.ShouldBe(Channels.Email);
        row.Ref.ShouldBe("reset:42");
        row.State.ShouldBe(OutboxState.Completed);
        row.ResultStatus.ShouldBe("sent");
        row.ProviderMessageId.ShouldNotBeNullOrEmpty();
        row.Envelope.ShouldBeNull();
    }

    private async Task<int> addUserAsync(string? email, string? fullName = null, short status = UserStatus.Active)
    {
        return await _fixture.QueryDbAsync(async db =>
        {
            var name = "mail-" + Guid.NewGuid().ToString("N");
            var user = new User { UserName = name, DisplayName = name, FullName = fullName ?? name, Email = email, StatusId = status, Password = "x" };
            db.Set<User>().Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        });
    }

    private Task waitForRowsAsync(System.Linq.Expressions.Expression<Func<OutboxMessage, bool>> done, int expected) =>
        NotificationRaiser.WaitUntilAsync(async () =>
            await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().Where(o => o.Channel == "email").Where(done).CountAsync()) >= expected);
}
