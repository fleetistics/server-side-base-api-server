using System.Net;
using System.Net.Http.Json;
using db_model.Gps;
using db_model.UserManagement;
using exs.modelCommons.AppStructure;
using exs.modelCommons.UserManagement;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>A phone that logs in by its DeviceUID is a gps_device of provider Mobile, typed by its platform.</summary>
[Collection(ApiCollection.Name)]
public sealed class AutoDeviceGpsDeviceTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;

    public AutoDeviceGpsDeviceTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static object ClientInfo(string deviceUid, short platformId) => new
    {
        AppUID = "test-app-uid",
        AppVersion = "1.0.0",
        DeviceUID = deviceUid,
        CodeVersion = "1",
        PlatformId = platformId,
        FCM_FID = "test-fcm",
    };

    private Task<List<GpsDevice>> DevicesAsync(string serial) => _fixture.QueryDbAsync(db =>
        db.Set<GpsDevice>().AsNoTracking().Where(d => d.SerialNumber == serial).ToListAsync());

    [Theory]
    [InlineData(ClientDevicePlatform.Android, GpsDeviceType.MobileAndroid)]
    [InlineData(ClientDevicePlatform.Ios, GpsDeviceType.MobileIos)]
    public async Task FirstLogin_CreatesMobileDeviceTypedByPlatform(short platformId, int expectedTypeId)
    {
        var uid = Guid.NewGuid().ToString("N");

        var response = await _fixture.CreateClient().PostAsJsonAsync("/api/auth_auto/refresh", ClientInfo(uid, platformId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var device = (await DevicesAsync(uid)).ShouldHaveSingleItem();
        device.ProviderId.ShouldBe(GpsDeviceProvider.Mobile);
        device.TypeId.ShouldBe(expectedTypeId);
        device.StatusId.ShouldBe(GpsDeviceStatus.Active);
        device.UserId.ShouldNotBeNull();
        device.CompanyId.ShouldBeNull();
    }

    [Fact]
    public async Task UnknownPlatform_CreatesDeviceWithoutType()
    {
        var uid = Guid.NewGuid().ToString("N");

        (await _fixture.CreateClient().PostAsJsonAsync("/api/auth_auto/refresh", ClientInfo(uid, ClientDevicePlatform.Unknown)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await DevicesAsync(uid)).ShouldHaveSingleItem().TypeId.ShouldBeNull();
    }

    [Fact]
    public async Task LoginAfterSessionEnded_ReusesTheDevice()
    {
        var uid = Guid.NewGuid().ToString("N");
        (await _fixture.CreateClient().PostAsJsonAsync("/api/auth_auto/refresh", ClientInfo(uid, ClientDevicePlatform.Android)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        var first = (await DevicesAsync(uid)).ShouldHaveSingleItem();

        // The session is gone (logged out / expired) but the phone and its user remain: the next login must find
        // the device by its serial number instead of creating a second one.
        await _fixture.QueryDbAsync(async db =>
        {
            await db.Set<UserSession>().Where(s => s.ClientInfo.DeviceUID == uid)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.StatusId, UserSessionStatus.LoggedOutByUser));
            return 0;
        });

        (await _fixture.CreateClient().PostAsJsonAsync("/api/auth_auto/refresh", ClientInfo(uid, ClientDevicePlatform.Android)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var devices = await DevicesAsync(uid);
        devices.ShouldHaveSingleItem().Id.ShouldBe(first.Id);
    }
}
