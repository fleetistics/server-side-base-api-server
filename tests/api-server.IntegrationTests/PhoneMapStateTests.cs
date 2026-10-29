using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using api_server.Controllers.MapData.Services;
using db_model.Gps;
using db_model.Map;
using exs.modelCommons.AppStructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>Phones write the same gps_device_map_state / gps_device_state_data tables the feeders fill for trackers.</summary>
public sealed class PhoneMapStateRulesTests
{
    [Theory]
    [InlineData(10f, null, GpsDeviceActivityStatus.Moving)]
    [InlineData(3.1f, MobileDeviceMotionActivity.Still, GpsDeviceActivityStatus.Moving)]
    [InlineData(3f, MobileDeviceMotionActivity.InVehicle, GpsDeviceActivityStatus.Idle)]
    [InlineData(0f, MobileDeviceMotionActivity.InVehicle, GpsDeviceActivityStatus.Idle)]
    [InlineData(0f, MobileDeviceMotionActivity.Still, GpsDeviceActivityStatus.Parked)]
    [InlineData(null, null, GpsDeviceActivityStatus.Parked)]
    public void Status_comes_from_speed_then_motion_activity(float? speedKmh, short? activity, byte expected) =>
        PhoneMapState.StatusFor(speedKmh, activity).ShouldBe(expected);

    [Fact]
    public void A_phone_clock_far_ahead_of_the_server_is_not_trusted()
    {
        var received = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

        PhoneMapState.TrustedPositionAt(received.AddSeconds(-30), received).ShouldBe(received.AddSeconds(-30));
        PhoneMapState.TrustedPositionAt(received.AddMinutes(4), received).ShouldBe(received.AddMinutes(4));
        PhoneMapState.TrustedPositionAt(received.AddDays(2), received).ShouldBe(received);
    }

    [Fact]
    public void Values_are_read_from_the_state_data_json_and_tolerate_missing_or_broken_data()
    {
        PhoneMapState.ReadValues("""{"battery":{"v":0.8,"t":"2026-10-28T12:00:00Z"},"activity":{"v":6,"t":"2026-10-28T12:00:00Z"}}""")
            .ShouldBe(((short?)6, (float?)0.8f));
        PhoneMapState.ReadValues("""{"charging":{"v":true,"t":"x"}}""").ShouldBe(((short?)null, (float?)null));
        PhoneMapState.ReadValues(null).ShouldBe(((short?)null, (float?)null));
        PhoneMapState.ReadValues("not json").ShouldBe(((short?)null, (float?)null));
    }
}

[Collection(ApiCollection.Name)]
public sealed class PhoneMapStateFlowTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;

    public PhoneMapStateFlowTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Logs a new phone in by its DeviceUID and returns a client carrying its token, plus its gps_device id.</summary>
    private async Task<(HttpClient Client, int DeviceId)> PhoneAsync()
    {
        var uid = Guid.NewGuid().ToString("N");
        var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth_auto/refresh", new
        {
            AppUID = "test-app-uid", AppVersion = "1.0.0", DeviceUID = uid, CodeVersion = "1",
            PlatformId = ClientDevicePlatform.Android, FCM_FID = "test-fcm",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = await response.Content.ReadFromJsonAsync<string>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var deviceId = await _fixture.QueryDbAsync(db => db.Set<GpsDevice>().Where(d => d.SerialNumber == uid).Select(d => d.Id).SingleAsync());
        return (client, deviceId);
    }

    // [lat, lon, accuracy m, speed m/s, heading, activity, time, altitude, battery, charging]
    private static object[] Fix(double lat, double lon, float speedMs, string activity, DateTime time, float battery, bool charging) =>
        [lat, lon, 5.0, speedMs, 90, activity, time.ToString("O"), 10.0, battery, charging];

    private async Task<T> WaitForAsync<T>(Func<Task<T?>> read) where T : class
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < until)
        {
            var value = await read();
            if (value != null) return value;
            await Task.Delay(100);
        }
        throw new TimeoutException("the background worker did not write the expected rows in 15 s");
    }

    [Fact]
    public async Task A_location_report_fills_the_phones_map_state_and_state_data()
    {
        var (client, deviceId) = await PhoneAsync();
        var time = DateTime.UtcNow.AddSeconds(-5);

        var response = await client.PostAsJsonAsync("/api/mobile-gps/location-report",
            new[] { Fix(50.45, 30.52, 10f, "in_vehicle", time, 0.8f, true) });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var state = await WaitForAsync(() => _fixture.QueryDbAsync(db =>
            db.Set<GpsDeviceMapState>().AsNoTracking().SingleOrDefaultAsync(s => s.GpsDeviceId == deviceId)));
        state.Location.Y.ShouldBe(50.45, 1e-9);
        state.Location.X.ShouldBe(30.52, 1e-9);
        state.Speed.ShouldNotBeNull().ShouldBe(36f, 0.01f);    // km/h, as the feeders store it
        state.ActivityStatusId.ShouldBe(GpsDeviceActivityStatus.Moving);
        state.StatusId.ShouldBe(GpsDeviceMapStatus.Visible);
        state.WarningFlags.ShouldBe(GpsDeviceWarning.None);
        state.LocationAccuracyId.ShouldBe(GPSLocationAccuracy.Fair); // the fix says 5 m: Fine is under 5
        state.CompanyId.ShouldBeNull();
        state.LastPositionAt.ShouldBe(time, TimeSpan.FromMilliseconds(1));

        var data = await _fixture.QueryDbAsync(db => db.Set<GpsDeviceStateData>().AsNoTracking().SingleAsync(d => d.GpsDeviceId == deviceId));
        var (activity, battery) = PhoneMapState.ReadValues(data.Data);
        activity.ShouldBe(MobileDeviceMotionActivity.InVehicle);
        battery.ShouldBe(0.8f);

        var parameters = DeviceStateData.Parse(data.Data);
        parameters.ActivityStatus!.Value.ShouldBe((short)GpsDeviceActivityStatus.Moving);
        parameters.ActivityStatus.Since.ShouldBe(time, TimeSpan.FromMilliseconds(1));
        parameters.Communication!.Value.ShouldBeTrue();
        parameters.Communication.At.ShouldBe(time, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task A_phone_marked_StalePosition_leaves_it_with_its_next_position_and_the_change_is_dated()
    {
        var (client, deviceId) = await PhoneAsync();
        var first = DateTime.UtcNow.AddMinutes(-30);
        (await client.PostAsJsonAsync("/api/mobile-gps/location-report",
            new[] { Fix(50.45, 30.52, 0f, "still", first, 0.8f, false) })).StatusCode.ShouldBe(HttpStatusCode.OK);
        await WaitForAsync(() => _fixture.QueryDbAsync(db =>
            db.Set<GpsDeviceMapState>().AsNoTracking().SingleOrDefaultAsync(s => s.GpsDeviceId == deviceId)));
        // What the feeders' sweep does to a device that stopped reporting: a status, and a date "now" for it.
        var swept = DateTime.UtcNow;
        await _fixture.QueryDbAsync(async db =>
        {
            var state = await db.Set<GpsDeviceMapState>().SingleAsync(s => s.GpsDeviceId == deviceId);
            state.ActivityStatusId = GpsDeviceActivityStatus.StalePosition;
            var data = await db.Set<GpsDeviceStateData>().SingleAsync(d => d.GpsDeviceId == deviceId);
            var parameters = DeviceStateData.Parse(data.Data);
            parameters.ActivityStatus = new StateValue<short>(GpsDeviceActivityStatus.StalePosition, swept, swept);
            data.Data = parameters.ToJson();
            await db.SaveChangesAsync();
            return 0;
        });

        // The phone reports again, with a fix whose own time is older than the moment the sweep ran.
        var again = first.AddMinutes(10);
        (await client.PostAsJsonAsync("/api/mobile-gps/location-report",
            new[] { Fix(50.46, 30.52, 0f, "in_vehicle", again, 0.7f, false) })).StatusCode.ShouldBe(HttpStatusCode.OK);
        await WaitForAsync(async () => await _fixture.QueryDbAsync(db =>
            db.Set<GpsDeviceMapState>().AsNoTracking().Where(s => s.GpsDeviceId == deviceId && s.ActivityStatusId != GpsDeviceActivityStatus.StalePosition).SingleOrDefaultAsync()));

        var state = await _fixture.QueryDbAsync(db => db.Set<GpsDeviceMapState>().AsNoTracking().SingleAsync(s => s.GpsDeviceId == deviceId));
        state.ActivityStatusId.ShouldBe(GpsDeviceActivityStatus.Idle); // in a vehicle, not moving
        var data = await _fixture.QueryDbAsync(db => db.Set<GpsDeviceStateData>().AsNoTracking().SingleAsync(d => d.GpsDeviceId == deviceId));
        var status = DeviceStateData.Parse(data.Data).ActivityStatus!;
        status.Value.ShouldBe((short)GpsDeviceActivityStatus.Idle); // the entry follows the column even though the sweep's date was newer
        status.Since.ShouldBe(again, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task A_late_batch_of_older_points_does_not_move_the_phone_back()
    {
        var (client, deviceId) = await PhoneAsync();
        var now = DateTime.UtcNow;

        (await client.PostAsJsonAsync("/api/mobile-gps/location-report",
            new[] { Fix(50.45, 30.52, 0f, "still", now.AddSeconds(-10), 0.8f, false) })).StatusCode.ShouldBe(HttpStatusCode.OK);
        await WaitForAsync(() => _fixture.QueryDbAsync(db =>
            db.Set<GpsDeviceMapState>().AsNoTracking().SingleOrDefaultAsync(s => s.GpsDeviceId == deviceId)));

        // The older batch is processed (its track point is stored) but must not touch the current state.
        (await client.PostAsJsonAsync("/api/mobile-gps/location-report",
            new[] { Fix(10.0, 10.0, 5f, "walking", now.AddSeconds(-120), 0.9f, false) })).StatusCode.ShouldBe(HttpStatusCode.OK);
        await WaitForAsync(async () => await _fixture.QueryDbAsync(db =>
            db.Set<MobileGpsDeviceTrackPoint>().CountAsync(p => p.MobileGpsDeviceId == deviceId)) == 2 ? "both stored" : null);

        var state = await _fixture.QueryDbAsync(db => db.Set<GpsDeviceMapState>().AsNoTracking().SingleAsync(s => s.GpsDeviceId == deviceId));
        state.Location.Y.ShouldBe(50.45, 1e-9);
        state.ActivityStatusId.ShouldBe(GpsDeviceActivityStatus.Parked);
        var data = await _fixture.QueryDbAsync(db => db.Set<GpsDeviceStateData>().AsNoTracking().SingleAsync(d => d.GpsDeviceId == deviceId));
        PhoneMapState.ReadValues(data.Data).BatteryLevel.ShouldBe(0.8f);
    }
}
