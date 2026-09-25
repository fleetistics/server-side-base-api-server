using System.Net;
using System.Net.Http.Json;
using exs.modelCommons.UserManagement;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthFlowTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;

    public AuthFlowTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // /api/auth/refresh (and /api/auth_auto/refresh) require a ClientSideInfo body —
    // there is no EmptyBodyBehavior.Allow here, so every call below must send one.
    private static readonly object SampleClientSideInfo = new
    {
        AppUid = "test-app-uid",
        AppVersion = "1.0.0",
        DeviceUID = "test-device-uid",
        CodeVersion = "1",
        PlatformName = "ios",
        FCMToken = "test-fcm-token",
    };

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndSetsRefreshCookie()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<ApiFixture.LoginResponse>();
        session!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        session.UserId.ShouldBe(_fixture.TestUserId);

        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        setCookie.ShouldContain("mf_refresh_token=");
        setCookie.ToLowerInvariant().ShouldContain("httponly");
        // Scoped so ordinary API calls never carry the rotation key.
        setCookie.ToLowerInvariant().ShouldContain("path=/api/auth");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = "wrong-password" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_WithValidCookie_ReturnsNewTokenAndRotatesKey()
    {
        // CreateClient handles cookies, so the login response's rotation key
        // travels automatically — same as a browser.
        var client = _fixture.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        var firstCookie = login.Headers.GetValues("Set-Cookie").Single();

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", SampleClientSideInfo);

        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        // AuthController inherits AuthAPIController's [Produces("application/json")]
        // (see AuthController.cs), so Ok(string) serializes as a genuine JSON string
        // now, not the raw text/plain body MVC's StringOutputFormatter used to emit —
        // matching every other endpoint in the API. Harmless for the real SPA: RTK
        // Query's fetchBaseQuery picks its parser from Content-Type, and .json() on a
        // JSON string literal returns the same unquoted string .text() used to.
        var newToken = await refresh.Content.ReadFromJsonAsync<string>();
        newToken.ShouldNotBeNullOrWhiteSpace();
        newToken.ShouldStartWith("eyJ"); // base64url JWT header

        var rotatedCookie = refresh.Headers.GetValues("Set-Cookie").Single();
        rotatedCookie.ShouldNotBe(firstCookie); // key must rotate on every refresh
    }

    [Fact]
    public async Task Refresh_WithReplayedOldRotationKey_IsRejected()
    {
        // Cookie handling off: this test manages the rotation keys by hand to
        // replay a key that was already rotated away (stolen-cookie scenario).
        var client = _fixture.CreateClient(new() { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        var originalKey = ExtractRotationKey(login);

        // First use of the key succeeds and rotates it...
        var first = await client.SendAsync(RefreshRequestWithKey(originalKey));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ...push the rotation moment well past the reuse-grace window (JwtOptions.
        // RefreshReuseGraceSeconds) so this is unambiguously a stale replay, not a race.
        await BackdateLatestRefreshTimeAsync(TimeSpan.FromMinutes(5));

        var replayed = await client.SendAsync(RefreshRequestWithKey(originalKey));
        replayed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithImmediatelyReplayedKey_IsToleratedAsDuplicate()
    {
        // Cookie handling off: this test manages the rotation keys by hand.
        var client = _fixture.CreateClient(new() { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        var originalKey = ExtractRotationKey(login);

        var first = await client.SendAsync(RefreshRequestWithKey(originalKey));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotatedKey = ExtractRotationKey(first);

        // Immediately replaying the just-superseded key (e.g. a second tab racing the
        // same rotation) must succeed, and must NOT rotate again — the caller gets
        // resynced to the same current key, not a third one.
        var replayed = await client.SendAsync(RefreshRequestWithKey(originalKey));
        replayed.StatusCode.ShouldBe(HttpStatusCode.OK);
        ExtractRotationKey(replayed).ShouldBe(rotatedKey);
    }

    private async Task BackdateLatestRefreshTimeAsync(TimeSpan by) =>
        await _fixture.QueryDbAsync(async db =>
        {
            var session = await db.Set<UserSession>().SingleAsync(s => s.UserId == _fixture.TestUserId);
            session.Token.LatestRefreshTime = DateTime.UtcNow - by;
            await db.SaveChangesAsync();
            return 0;
        });

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/refresh", SampleClientSideInfo);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithBearerToken_ReturnsUserId()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        body!.UserId.ShouldBe(_fixture.TestUserId.ToString());
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string ExtractRotationKey(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        var value = setCookie.Split(';')[0]; // "mf_refresh_token=<key>"
        return value["mf_refresh_token=".Length..];
    }

    private static HttpRequestMessage RefreshRequestWithKey(string rotationKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(SampleClientSideInfo),
        };
        request.Headers.Add("Cookie", $"mf_refresh_token={rotationKey}");
        return request;
    }

    private sealed record MeResponse(string UserId, string SessionId);
}
