using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using api_server.Auth;
using db_model.UserManagement;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>Per-account lock after repeated failed logins, and the log lines failed logins leave.</summary>
[Collection(ApiCollection.Name)]
public sealed class LoginThrottleTests : IAsyncLifetime
{
    private const string GoodPassword = "Correct-horse-battery-9";

    private readonly ApiFixture _fixture;
    private readonly FakeTimeProvider _time = new();
    private readonly CapturingSink _log = new();

    public LoginThrottleTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ---- locking -------------------------------------------------------------------------

    [Fact]
    public async Task AfterFiveFailures_TheAccountIsLocked_EvenForTheRightPassword()
    {
        await using var factory = ThrottledFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            (await LoginAsync(client, ApiFixture.TestUserName, "wrong-password")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        var locked = await LoginAsync(client, ApiFixture.TestUserName, ApiFixture.TestPassword);

        locked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        locked.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ShouldBe(15 * 60, 5);
        locked.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task AnUnknownUserNameLocksExactlyLikeARealOne()
    {
        await using var factory = ThrottledFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            (await LoginAsync(client, "ghost.user", "wrong-password")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        var locked = await LoginAsync(client, "ghost.user", "wrong-password");

        locked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        locked.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ShouldBe(15 * 60, 5);
    }

    [Fact]
    public async Task ALockedAccountDoesNotAffectOthers()
    {
        await AddUserAsync("other.user", GoodPassword);
        await using var factory = ThrottledFactory();
        var client = factory.CreateClient();
        await FailAsync(client, ApiFixture.TestUserName, 5);

        (await LoginAsync(client, ApiFixture.TestUserName, ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await LoginAsync(client, "other.user", GoodPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FourFailures_AreNotEnough()
    {
        await using var factory = ThrottledFactory();
        var client = factory.CreateClient();
        await FailAsync(client, ApiFixture.TestUserName, 4);

        (await LoginAsync(client, ApiFixture.TestUserName, ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ASuccessfulLogin_ClearsTheFailureCount()
    {
        await using var factory = ThrottledFactory();
        var client = factory.CreateClient();
        await FailAsync(client, ApiFixture.TestUserName, 4);
        (await LoginAsync(client, ApiFixture.TestUserName, ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await FailAsync(client, ApiFixture.TestUserName, 4); // would be 8 without the reset

        (await LoginAsync(client, ApiFixture.TestUserName, ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FailuresOutsideTheWindow_DoNotAddUp()
    {
        await using var factory = ThrottledFactory();
        var client = factory.CreateClient();
        await FailAsync(client, ApiFixture.TestUserName, 4);
        _time.Advance(TimeSpan.FromMinutes(16)); // window is 15 minutes

        await FailAsync(client, ApiFixture.TestUserName, 4);

        (await LoginAsync(client, ApiFixture.TestUserName, ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheLockEndsAfterTheLockoutPeriod()
    {
        await using var factory = ThrottledFactory();
        var client = factory.CreateClient();
        await FailAsync(client, ApiFixture.TestUserName, 5);

        _time.Advance(TimeSpan.FromMinutes(14));
        (await LoginAsync(client, ApiFixture.TestUserName, ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        _time.Advance(TimeSpan.FromMinutes(2));
        (await LoginAsync(client, ApiFixture.TestUserName, ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AfterTheLockEnds_CountingStartsOver()
    {
        await using var factory = ThrottledFactory();
        var client = factory.CreateClient();
        await FailAsync(client, ApiFixture.TestUserName, 5);
        _time.Advance(TimeSpan.FromMinutes(16));

        await FailAsync(client, ApiFixture.TestUserName, 4); // one fewer than needed to lock again

        (await LoginAsync(client, ApiFixture.TestUserName, ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SettingTheLimitToZero_TurnsTheLockOff()
    {
        var client = _fixture.CreateClient(); // fixture default: throttle off
        await FailAsync(client, ApiFixture.TestUserName, 12);

        (await LoginAsync(client, ApiFixture.TestUserName, ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EmailMode_CountsTheNameCaseInsensitively()
    {
        await AddUserAsync("Mixed.Case@Example.test", GoodPassword);
        await using var factory = ThrottledFactory(b => b.UseSetting("Auth:EmailMode", "UniqueEmail"));
        var client = factory.CreateClient();
        string[] spellings = ["mixed.case@example.test", "MIXED.CASE@EXAMPLE.TEST", "Mixed.Case@Example.test", " mixed.case@example.test ", "mIxEd.cAsE@eXaMpLe.TeSt"];
        foreach (var spelling in spellings)
        {
            (await LoginAsync(client, spelling, "wrong-password")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await LoginAsync(client, "Mixed.Case@example.TEST", GoodPassword)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task TheLockoutPeriodAndLimitComeFromConfiguration()
    {
        await using var factory = ThrottledFactory(b =>
        {
            b.UseSetting("Auth:LoginMaxFailedAttempts", "2");
            b.UseSetting("Auth:LoginLockoutMinutes", "3");
        });
        var client = factory.CreateClient();
        await FailAsync(client, ApiFixture.TestUserName, 2);

        var locked = await LoginAsync(client, ApiFixture.TestUserName, ApiFixture.TestPassword);

        locked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        locked.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ShouldBe(3 * 60, 5);
    }

    [Fact]
    public async Task SettingANewPasswordWithAResetKey_LiftsTheLock()
    {
        await using var factory = ThrottledFactory();
        var client = factory.CreateClient();
        await FailAsync(client, ApiFixture.TestUserName, 5);
        (await LoginAsync(client, ApiFixture.TestUserName, ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        await client.PostAsJsonAsync("/api/auth/request-password-reset", new { Identifier = ApiFixture.TestUserName });
        var key = _fixture.ResetKeySender.LatestKeyFor(_fixture.TestUserId)!;
        (await client.PostAsJsonAsync("/api/auth/reset-password", new { ResetKey = key, NewPassword = GoodPassword }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await LoginAsync(client, ApiFixture.TestUserName, GoodPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---- logging -------------------------------------------------------------------------

    [Fact]
    public async Task AFailedLogin_IsLogged_WithNameReasonAndCaller()
    {
        var client = _fixture.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<ILogEventSink>(_log))).CreateClient();

        await LoginAsync(client, ApiFixture.TestUserName, "wrong-password");
        await LoginAsync(client, "ghost.user", "wrong-password");

        var wrong = FailedLoginEvents().Single(e => e.RenderMessage().Contains("test.user"));
        wrong.Level.ShouldBe(LogEventLevel.Warning);
        wrong.RenderMessage().ShouldContain("WrongPassword");
        wrong.Properties.ContainsKey("ClientIp").ShouldBeTrue();
        wrong.Properties["UserId"].ToString().ShouldBe(_fixture.TestUserId.ToString());

        var unknown = FailedLoginEvents().Single(e => e.RenderMessage().Contains("ghost.user"));
        unknown.RenderMessage().ShouldContain("UnknownOrInactiveUser");
    }

    [Fact]
    public async Task TheTypedNameIsSanitisedInTheLog()
    {
        var client = _fixture.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<ILogEventSink>(_log))).CreateClient();
        var hostile = "evil\r\n2026-01-01 FAKE LOG LINE\t" + new string('x', 300);

        await LoginAsync(client, hostile, "wrong-password");

        var message = FailedLoginEvents().Single().RenderMessage();
        message.ShouldNotContain("\n");
        message.ShouldNotContain("\r");
        message.ShouldNotContain("\t");
        message.Length.ShouldBeLessThan(400); // the 300 x's were cut
    }

    [Fact]
    public async Task TheLockAndEveryRefusalWhileLockedAreLogged()
    {
        await using var factory = ThrottledFactory(b => b.ConfigureServices(s => s.AddSingleton<ILogEventSink>(_log)));
        var client = factory.CreateClient();

        await FailAsync(client, ApiFixture.TestUserName, 5);
        await LoginAsync(client, ApiFixture.TestUserName, ApiFixture.TestPassword);

        var messages = _log.Events.Select(e => e.RenderMessage()).ToList();
        messages.Count(m => m.Contains("Failed login attempt")).ShouldBe(5);
        messages.ShouldContain(m => m.Contains("failure 5 of 5"));
        messages.Count(m => m.Contains("locked for 15 min after 5 failed login attempts")).ShouldBe(1);
        messages.Count(m => m.Contains("Login refused") && m.Contains("account locked")).ShouldBe(1);
    }

    // ---- helpers -------------------------------------------------------------------------

    private WebApplicationFactory<Program> ThrottledFactory(Action<Microsoft.AspNetCore.Hosting.IWebHostBuilder>? more = null) =>
        _fixture.WithWebHostBuilder(b =>
        {
            b.UseSetting("Auth:LoginMaxFailedAttempts", "5");
            b.ConfigureServices(s => s.AddSingleton<TimeProvider>(_time));
            more?.Invoke(b);
        });

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string userName, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { UserName = userName, Password = password });

    private static async Task FailAsync(HttpClient client, string userName, int times)
    {
        for (var i = 0; i < times; i++)
        {
            (await LoginAsync(client, userName, "wrong-password")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    private List<LogEvent> FailedLoginEvents() =>
        _log.Events.Where(e => e.RenderMessage().Contains("Failed login attempt")).ToList();

    private Task AddUserAsync(string userName, string password) =>
        _fixture.QueryDbAsync(async db =>
        {
            db.Add(new User
            {
                UserName = userName,
                DisplayName = userName,
                FullName = userName,
                Password = new PasswordHasher().Hash(password),
                StatusId = UserStatus.Active,
            });
            await db.SaveChangesAsync();
            return 0;
        });

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class CapturingSink : ILogEventSink
    {
        private readonly ConcurrentQueue<LogEvent> _events = new();
        public IEnumerable<LogEvent> Events => _events;
        public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
    }
}
