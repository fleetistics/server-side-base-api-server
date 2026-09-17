using api_server.Services.Notifications;
using db_model.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// NotificationService/INotifier is a BackgroundService: QueueNotificationAsync only enqueues to
/// an in-memory channel, the actual Notification/NotificationToUser/NotificationQueue rows are
/// written asynchronously by the service's own processing loop. These tests poll the database for
/// the expected state rather than asserting immediately after the call returns.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class NotificationServiceTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;

    public NotificationServiceTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task QueueNotificationAsync_PersistsNotificationAndFansOutOneQueueRowPerUser()
    {
        var notifier = _fixture.Services.GetRequiredService<INotifier>();
        var notification = new Notification
        {
            Date = DateTime.UtcNow,
            TypeId = 7,
            EntityId = 123,
            Title = "Title",
            Body = "Body",
            Payload = """{"foo":"bar"}""",
        };
        var userIds = new[] { 1001, 1002 };

        await notifier.QueueNotificationAsync(notification, userIds, CancellationToken.None);

        await waitUntilAsync(async () => await _fixture.QueryDbAsync(db => db.Set<NotificationQueue>().CountAsync()) == 2);

        var savedNotification = await _fixture.QueryDbAsync(db => db.Set<Notification>().AsNoTracking().SingleAsync());
        savedNotification.TypeId.ShouldBe((short)7);
        savedNotification.EntityId.ShouldBe(123);
        savedNotification.Title.ShouldBe("Title");
        savedNotification.Body.ShouldBe("Body");
        savedNotification.Payload.ShouldBe("""{"foo":"bar"}""");

        var toUserRows = await _fixture.QueryDbAsync(db => db.Set<NotificationToUser>().AsNoTracking().ToListAsync());
        toUserRows.Select(r => r.UserId).OrderBy(id => id).ShouldBe(userIds.OrderBy(id => id));
        toUserRows.ShouldAllBe(r => r.NotificationId == savedNotification.Id);

        var queueRows = await _fixture.QueryDbAsync(db => db.Set<NotificationQueue>().AsNoTracking().ToListAsync());
        queueRows.Count.ShouldBe(2);
        queueRows.Select(r => r.UserId).OrderBy(id => id).ShouldBe(userIds.OrderBy(id => id));
        queueRows.ShouldAllBe(r => r.NotificationId == savedNotification.Id);
        queueRows.ShouldAllBe(r => r.TransportType == NotificationTransportType.FCM);
    }

    [Fact]
    public async Task QueueNotificationAsync_DuplicateUserIdsInRequest_FansOutOnlyOncePerUser()
    {
        var notifier = _fixture.Services.GetRequiredService<INotifier>();
        var notification = new Notification { Date = DateTime.UtcNow, TypeId = 1, Title = "T", Body = "B" };

        await notifier.QueueNotificationAsync(notification, [2001, 2001, 2001], CancellationToken.None);

        // The whole batch (Notification + NotificationToUser + NotificationQueue rows) is written
        // in a single SaveChangesAsync, so as soon as any NotificationQueue row exists, the fan-out
        // for this call is already complete - no need to wait further before asserting the count.
        await waitUntilAsync(async () => await _fixture.QueryDbAsync(db => db.Set<NotificationQueue>().AnyAsync()));

        var queueRows = await _fixture.QueryDbAsync(db => db.Set<NotificationQueue>().AsNoTracking().ToListAsync());
        queueRows.ShouldHaveSingleItem();
        queueRows[0].UserId.ShouldBe(2001);

        var toUserRows = await _fixture.QueryDbAsync(db => db.Set<NotificationToUser>().AsNoTracking().ToListAsync());
        toUserRows.ShouldHaveSingleItem();
    }

    private static async Task waitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("NotificationService did not process the queued notification within the timeout.");
    }
}
