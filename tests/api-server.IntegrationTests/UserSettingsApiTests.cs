using System.Net;
using System.Net.Http.Json;
using db_model.UserManagement;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// Covers the tiered fallback (exact app+platform &gt; app &gt; user &gt; global), the
/// cascading delete of narrower overrides when a broader-scoped value is PUT, and the
/// null-value "reset" marker falling through instead of masking a broader tier.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class UserSettingsApiTests : IAsyncLifetime
{
    private const short AppId = 5;
    private const short PlatformId = 1;

    private readonly ApiFixture _fixture;

    public UserSettingsApiTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetUserSettings_WithoutToken_Returns401()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync($"/api/users/me/settings?clientApplicationId={AppId}&clientDevicePlatformId={PlatformId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutUserSetting_WithoutToken_Returns401()
    {
        var client = _fixture.CreateClient();

        var response = await client.PutAsJsonAsync("/api/users/me/settings", new { Name = "theme", Value = "\"dark\"" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutUserSetting_NewSetting_CreatesRowScopedToCurrentUser()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/users/me/settings",
            new { Name = "theme", Value = "\"dark\"", ClientApplicationId = AppId, ClientDevicePlatformId = PlatformId });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SettingResponse>();
        body!.Name.ShouldBe("theme");
        body.Value.ShouldBe("\"dark\"");

        var stored = await _fixture.QueryDbAsync(db =>
            db.Set<UserSettings>().AsNoTracking().SingleAsync(s => s.Name == "theme"));
        stored.UserId.ShouldBe(_fixture.TestUserId);
        stored.ClientApplicationId.ShouldBe(AppId);
        stored.ClientDevicePlatformId.ShouldBe(PlatformId);
    }

    [Fact]
    public async Task PutUserSetting_ExistingSetting_UpdatesValueRatherThanDuplicating()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        await client.PutAsJsonAsync("/api/users/me/settings",
            new { Name = "theme", Value = "\"dark\"", ClientApplicationId = AppId, ClientDevicePlatformId = PlatformId });

        var response = await client.PutAsJsonAsync("/api/users/me/settings",
            new { Name = "theme", Value = "\"light\"", ClientApplicationId = AppId, ClientDevicePlatformId = PlatformId });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var matching = await _fixture.QueryDbAsync(db =>
            db.Set<UserSettings>().AsNoTracking().Where(s => s.Name == "theme").ToListAsync());
        matching.ShouldHaveSingleItem();
        matching[0].Value.ShouldBe("\"light\"");
    }

    [Fact]
    public async Task GetUserSettings_ReturnsMostSpecificTier_WhenAllTiersPresent()
    {
        await SeedSettingAsync("theme", "\"global\"", userId: null, clientApplicationId: null, clientDevicePlatformId: null);
        await SeedSettingAsync("theme", "\"user\"", userId: _fixture.TestUserId, clientApplicationId: null, clientDevicePlatformId: null);
        await SeedSettingAsync("theme", "\"app\"", userId: _fixture.TestUserId, clientApplicationId: AppId, clientDevicePlatformId: null);
        await SeedSettingAsync("theme", "\"full\"", userId: _fixture.TestUserId, clientApplicationId: AppId, clientDevicePlatformId: PlatformId);
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var settings = await GetSettingsAsync(client);

        settings.Single(s => s.Name == "theme").Value.ShouldBe("\"full\"");
    }

    [Fact]
    public async Task GetUserSettings_FallsBackToAppTier_WhenPlatformSpecificMissing()
    {
        await SeedSettingAsync("theme", "\"global\"", userId: null, clientApplicationId: null, clientDevicePlatformId: null);
        await SeedSettingAsync("theme", "\"user\"", userId: _fixture.TestUserId, clientApplicationId: null, clientDevicePlatformId: null);
        await SeedSettingAsync("theme", "\"app\"", userId: _fixture.TestUserId, clientApplicationId: AppId, clientDevicePlatformId: null);
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var settings = await GetSettingsAsync(client);

        settings.Single(s => s.Name == "theme").Value.ShouldBe("\"app\"");
    }

    [Fact]
    public async Task GetUserSettings_FallsBackToUserTier_WhenAppTierMissing()
    {
        await SeedSettingAsync("theme", "\"global\"", userId: null, clientApplicationId: null, clientDevicePlatformId: null);
        await SeedSettingAsync("theme", "\"user\"", userId: _fixture.TestUserId, clientApplicationId: null, clientDevicePlatformId: null);
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var settings = await GetSettingsAsync(client);

        settings.Single(s => s.Name == "theme").Value.ShouldBe("\"user\"");
    }

    [Fact]
    public async Task GetUserSettings_FallsBackToGlobalTier_WhenNoUserRowsExist()
    {
        await SeedSettingAsync("theme", "\"global\"", userId: null, clientApplicationId: null, clientDevicePlatformId: null);
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var settings = await GetSettingsAsync(client);

        settings.Single(s => s.Name == "theme").Value.ShouldBe("\"global\"");
    }

    [Fact]
    public async Task GetUserSettings_NullValueAtClosestTier_FallsThroughToBroaderTier()
    {
        // A null Value at a tier is a "reset" marker, not a real override — GET must skip
        // it and keep looking at broader tiers instead of treating it as the answer.
        await SeedSettingAsync("theme", "\"global\"", userId: null, clientApplicationId: null, clientDevicePlatformId: null);
        await SeedSettingAsync("theme", null, userId: _fixture.TestUserId, clientApplicationId: AppId, clientDevicePlatformId: PlatformId);
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var settings = await GetSettingsAsync(client);

        settings.Single(s => s.Name == "theme").Value.ShouldBe("\"global\"");
    }

    [Fact]
    public async Task PutUserSetting_ValueNull_ResetsClosestTier_AndGetFallsBackToBroaderTier()
    {
        await SeedSettingAsync("theme", "\"global\"", userId: null, clientApplicationId: null, clientDevicePlatformId: null);
        var client = await _fixture.CreateAuthenticatedClientAsync();
        await client.PutAsJsonAsync("/api/users/me/settings",
            new { Name = "theme", Value = "\"full\"", ClientApplicationId = AppId, ClientDevicePlatformId = PlatformId });

        var response = await client.PutAsJsonAsync("/api/users/me/settings",
            new { Name = "theme", Value = (string?)null, ClientApplicationId = AppId, ClientDevicePlatformId = PlatformId });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settings = await GetSettingsAsync(client);
        settings.Single(s => s.Name == "theme").Value.ShouldBe("\"global\"");
    }

    [Fact]
    public async Task PutUserSetting_AppLevelValue_DeletesNarrowerPlatformSpecificOverride()
    {
        await SeedSettingAsync("theme", "\"full\"", userId: _fixture.TestUserId, clientApplicationId: AppId, clientDevicePlatformId: PlatformId);
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/users/me/settings",
            new { Name = "theme", Value = "\"appDefault\"", ClientApplicationId = AppId, ClientDevicePlatformId = (short?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var remaining = await _fixture.QueryDbAsync(db =>
            db.Set<UserSettings>().AsNoTracking().Where(s => s.Name == "theme" && s.UserId == _fixture.TestUserId).ToListAsync());
        remaining.ShouldHaveSingleItem();
        remaining[0].ClientDevicePlatformId.ShouldBeNull();
        remaining[0].Value.ShouldBe("\"appDefault\"");

        var settings = await GetSettingsAsync(client);
        settings.Single(s => s.Name == "theme").Value.ShouldBe("\"appDefault\"");
    }

    [Fact]
    public async Task PutUserSetting_UserLevelValue_DeletesAllAppSpecificOverrides()
    {
        await SeedSettingAsync("theme", "\"app\"", userId: _fixture.TestUserId, clientApplicationId: AppId, clientDevicePlatformId: null);
        await SeedSettingAsync("theme", "\"full\"", userId: _fixture.TestUserId, clientApplicationId: AppId, clientDevicePlatformId: PlatformId);
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/users/me/settings",
            new { Name = "theme", Value = "\"userDefault\"", ClientApplicationId = (short?)null, ClientDevicePlatformId = (short?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var remaining = await _fixture.QueryDbAsync(db =>
            db.Set<UserSettings>().AsNoTracking().Where(s => s.Name == "theme" && s.UserId == _fixture.TestUserId).ToListAsync());
        remaining.ShouldHaveSingleItem();
        remaining[0].ClientApplicationId.ShouldBeNull();
        remaining[0].Value.ShouldBe("\"userDefault\"");
    }

    [Fact]
    public async Task GetUserSettings_OnlyReturnsSettingsForRequestingUser()
    {
        var otherUserId = await _fixture.QueryDbAsync(async db =>
        {
            var otherUser = new User
            {
                UserName = "other.user",
                Password = "irrelevant",
                DisplayName = "Other",
                FullName = "Other User",
                Email = "other.user@example.test",
                Phone = "8135550101",
                StatusId = UserStatus.Active,
            };
            db.Add(otherUser);
            await db.SaveChangesAsync();
            return otherUser.Id;
        });
        await SeedSettingAsync("theme", "\"other-users-value\"", userId: otherUserId, clientApplicationId: AppId, clientDevicePlatformId: PlatformId);
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var settings = await GetSettingsAsync(client);

        settings.ShouldNotContain(s => s.Name == "theme");
    }

    private async Task<List<SettingResponse>> GetSettingsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<SettingResponse>>(
            $"/api/users/me/settings?clientApplicationId={AppId}&clientDevicePlatformId={PlatformId}"))!;

    private async Task SeedSettingAsync(string name, string? value, int? userId, short? clientApplicationId, short? clientDevicePlatformId)
    {
        await _fixture.QueryDbAsync(async db =>
        {
            db.Set<UserSettings>().Add(new UserSettings
            {
                UserId = userId,
                Name = name,
                Value = value,
                ClientApplicationId = clientApplicationId,
                ClientDevicePlatformId = clientDevicePlatformId,
            });
            await db.SaveChangesAsync();
            return 0;
        });
    }

    private sealed record SettingResponse(string Name, string? Value);
}
