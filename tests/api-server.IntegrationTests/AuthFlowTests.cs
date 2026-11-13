using System.Net;
using System.Net.Http.Json;
using db_model.Gps;
using exs.modelCommons.AppStructure;
using exs.modelCommons.UserManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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

        // "{sessionId}.{counter}.{mac}": a fresh session starts at counter 0 and names itself.
        var (sessionId, counter) = ParseKey(ExtractRotationKey(response));
        sessionId.ShouldBe(session.SessionId);
        counter.ShouldBe(0);
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

    // 1
    [Fact]
    public async Task Refresh_WithValidCookie_ReturnsNewTokenAndRotatesKey()
    {
        // CreateClient handles cookies, so the login response's rotation key
        // travels automatically — same as a browser.
        var client = _fixture.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        var firstKey = ExtractRotationKey(login);

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

        var rotatedKey = ExtractRotationKey(refresh);
        rotatedKey.ShouldNotBe(firstKey); // key must rotate on every refresh
        ParseKey(rotatedKey).Counter.ShouldBe(ParseKey(firstKey).Counter + 1);
        ParseKey(rotatedKey).SessionId.ShouldBe(ParseKey(firstKey).SessionId);
    }

    // 3
    [Fact]
    public async Task Refresh_WithReplayedOldRotationKey_IsRejectedAndRevokesTheSession()
    {
        // Cookie handling off: this test manages the rotation keys by hand to
        // replay a key that was already rotated away (stolen-cookie scenario).
        var client = _fixture.CreateClient(new() { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        var originalKey = ExtractRotationKey(login);
        var sessionId = ParseKey(originalKey).SessionId;

        // First use of the key succeeds and rotates it...
        var first = await client.SendAsync(RefreshRequestWithKey(originalKey));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var currentKey = ExtractRotationKey(first);

        // ...push the rotation moment well past the reuse-grace window (JwtOptions.
        // RefreshReuseGraceSeconds) so this is unambiguously a stale replay, not a race.
        await BackdateLatestRefreshTimeAsync(sessionId, TimeSpan.FromMinutes(5));

        var replayed = await client.SendAsync(RefreshRequestWithKey(originalKey));
        replayed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The replay ended the session: the legitimate holder's current key is dead too.
        (await client.SendAsync(RefreshRequestWithKey(currentKey))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await SessionAsync(sessionId)).StatusId.ShouldBe(UserSessionStatus.RevokedKeyReuse);
    }

    // 2
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

        (await SessionAsync(ParseKey(originalKey).SessionId)).Token.RotationCounter.ShouldBe(1);
    }

    // 4
    [Fact]
    public async Task Refresh_WithKeyTwoRotationsOld_IsRejectedEvenInsideTheGraceWindow()
    {
        var client = _fixture.CreateClient(new() { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        var key0 = ExtractRotationKey(login);
        var key1 = ExtractRotationKey(await client.SendAsync(RefreshRequestWithKey(key0)));
        var key2 = ExtractRotationKey(await client.SendAsync(RefreshRequestWithKey(key1)));

        // The latest rotation is milliseconds old, yet only the immediately previous key counts as a race.
        (await client.SendAsync(RefreshRequestWithKey(key0))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await client.SendAsync(RefreshRequestWithKey(key2))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await SessionAsync(ParseKey(key0).SessionId)).StatusId.ShouldBe(UserSessionStatus.RevokedKeyReuse);
    }

    // 4a
    [Fact]
    public async Task Refresh_WithCounterAheadOfTheDatabase_IsRejectedAndRevokesTheSession()
    {
        var client = _fixture.CreateClient(new() { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        var key0 = ExtractRotationKey(login);
        var key1 = ExtractRotationKey(await client.SendAsync(RefreshRequestWithKey(key0)));
        var key2 = ExtractRotationKey(await client.SendAsync(RefreshRequestWithKey(key1)));
        var sessionId = ParseKey(key0).SessionId;

        // Simulates a restore from backup that lost the last rotations: key2 is now "from the future".
        await _fixture.QueryDbAsync(async db =>
        {
            var session = await db.Set<UserSession>().SingleAsync(s => s.Id == sessionId);
            session.Token.RotationCounter = 0;
            await db.SaveChangesAsync();
            return 0;
        });

        (await client.SendAsync(RefreshRequestWithKey(key2))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await SessionAsync(sessionId)).StatusId.ShouldBe(UserSessionStatus.RevokedKeyReuse);
    }

    // 5
    [Fact]
    public async Task Refresh_WithForgedMac_IsRejectedWithoutTouchingTheSession()
    {
        var client = _fixture.CreateClient(new() { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        var realKey = ExtractRotationKey(login);
        var (sessionId, counter) = ParseKey(realKey);

        var forgedMac = Convert.ToBase64String(new byte[32]).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        foreach (var forgedCounter in new[] { counter, counter + 1, 99 })
        {
            var response = await client.SendAsync(RefreshRequestWithKey($"{sessionId}.{forgedCounter}.{forgedMac}"));
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // Guessing a session id must not be able to log anyone out; the real key still works.
        (await SessionAsync(sessionId)).StatusId.ShouldBe(UserSessionStatus.Active);
        (await client.SendAsync(RefreshRequestWithKey(realKey))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // 6
    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("a.b.c")]
    [InlineData("-1.2.AAAA")]
    [InlineData("1.-2.AAAA")]
    [InlineData("1.2.!!!!")]
    [InlineData("99999999999999999999.1.AAAA")]
    [InlineData("1.2.AAAA")] // well-formed but the mac is too short
    public async Task Refresh_WithMalformedCookie_Returns401(string cookieValue)
    {
        var client = _fixture.CreateClient(new() { HandleCookies = false });

        var response = await client.SendAsync(RefreshRequestWithKey(cookieValue));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // 7
    [Fact]
    public async Task Refresh_FiredTwiceInParallelWithTheSameKey_BothSucceedWithTheSameCookie()
    {
        var client = _fixture.CreateClient(new() { HandleCookies = false });

        // Repeated: whether the two requests actually overlap in the database is up to timing.
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login",
                new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
            var key = ExtractRotationKey(login);

            var responses = await Task.WhenAll(
                client.SendAsync(RefreshRequestWithKey(key)),
                client.SendAsync(RefreshRequestWithKey(key)));

            responses[0].StatusCode.ShouldBe(HttpStatusCode.OK);
            responses[1].StatusCode.ShouldBe(HttpStatusCode.OK);
            var cookie0 = ExtractRotationKey(responses[0]);
            ExtractRotationKey(responses[1]).ShouldBe(cookie0);
            ParseKey(cookie0).Counter.ShouldBe(1);
            (await SessionAsync(ParseKey(key).SessionId)).Token.RotationCounter.ShouldBe(1);
        }
    }

    // 8
    [Fact]
    public async Task Login_CookieIsSecureUnlessInsecureCookiesAreAllowed()
    {
        // The fixture allows insecure cookies (plain http); a host without that setting must not.
        using var secureHost = _fixture.WithWebHostBuilder(b => b.UseSetting("Jwt:AllowInsecureRefreshCookie", "false"));
        var client = secureHost.CreateClient(new() { HandleCookies = false });

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant().ShouldContain("secure");

        var insecure = await _fixture.CreateClient(new() { HandleCookies = false }).PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        insecure.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant().ShouldNotContain("secure");
    }

    // 9
    [Fact]
    public async Task Logout_WithStaleCounterButValidMac_EndsTheSession()
    {
        var client = _fixture.CreateClient(new() { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        var key0 = ExtractRotationKey(login);
        await client.SendAsync(RefreshRequestWithKey(key0)); // key0 is now one rotation old
        var sessionId = ParseKey(key0).SessionId;

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", $"mf_refresh_token={key0}");
        (await client.SendAsync(logout)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await SessionAsync(sessionId)).StatusId.ShouldBe(UserSessionStatus.LoggedOutByUser);
    }

    [Fact]
    public async Task Logout_WithForgedMac_LeavesTheSessionActive()
    {
        var client = _fixture.CreateClient(new() { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        var sessionId = ParseKey(ExtractRotationKey(login)).SessionId;
        var forgedMac = Convert.ToBase64String(new byte[32]).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", $"mf_refresh_token={sessionId}.0.{forgedMac}");
        await client.SendAsync(logout);

        (await SessionAsync(sessionId)).StatusId.ShouldBe(UserSessionStatus.Active);
    }

    // 10
    [Fact]
    public async Task Revocation_OfAPhoneSession_MarksTheDeviceLoggedOut()
    {
        var client = _fixture.CreateClient(new() { HandleCookies = false });
        const string deviceUid = "revocation-test-phone";
        var login = await client.PostAsJsonAsync("/api/auth/app_login", new
        {
            UserName = ApiFixture.TestUserName,
            Password = ApiFixture.TestPassword,
            ClientSideInfo = new
            {
                AppUID = "test-app-uid",
                AppVersion = "1.0.0",
                DeviceUID = deviceUid,
                CodeVersion = "1",
                PlatformId = ClientDevicePlatform.Android,
                FCM_FID = "test-fcm",
            },
        });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var key0 = ExtractRotationKey(login);
        var sessionId = ParseKey(key0).SessionId;
        (await SessionAsync(sessionId)).MobileGpsDeviceId.ShouldNotBeNull();

        (await client.SendAsync(RefreshRequestWithKey(key0))).StatusCode.ShouldBe(HttpStatusCode.OK);
        await BackdateLatestRefreshTimeAsync(sessionId, TimeSpan.FromMinutes(5));
        (await client.SendAsync(RefreshRequestWithKey(key0))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await SessionAsync(sessionId)).StatusId.ShouldBe(UserSessionStatus.RevokedKeyReuse);
        var device = await _fixture.QueryDbAsync(db =>
            db.Set<GpsDevice>().AsNoTracking().SingleAsync(d => d.SerialNumber == deviceUid));
        device.StatusId.ShouldBe(GpsDeviceStatus.MobileLogOutInactive);
    }

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

    private Task<UserSession> SessionAsync(int sessionId) => _fixture.QueryDbAsync(db =>
        db.Set<UserSession>().AsNoTracking().SingleAsync(s => s.Id == sessionId));

    private async Task BackdateLatestRefreshTimeAsync(int sessionId, TimeSpan by) =>
        await _fixture.QueryDbAsync(async db =>
        {
            var session = await db.Set<UserSession>().SingleAsync(s => s.Id == sessionId);
            session.Token.LatestRefreshTime = DateTime.UtcNow - by;
            await db.SaveChangesAsync();
            return 0;
        });

    // The whole cookie value: "{sessionId}.{counter}.{mac}".
    private static string ExtractRotationKey(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        var value = setCookie.Split(';')[0]; // "mf_refresh_token=<key>"
        return value["mf_refresh_token=".Length..];
    }

    private static (int SessionId, int Counter) ParseKey(string key)
    {
        var parts = key.Split('.');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
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
