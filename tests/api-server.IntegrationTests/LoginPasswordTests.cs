using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using api_server.Auth;
using db_model.UserManagement;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>Login against Argon2id hashes, and that every failure costs the same.</summary>
[Collection(ApiCollection.Name)]
public sealed class LoginPasswordTests : IAsyncLifetime
{
    private const string GoodPassword = "Correct-horse-battery-9";

    private readonly ApiFixture _fixture;

    public LoginPasswordTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ---- outcomes ------------------------------------------------------------------------

    [Fact]
    public async Task Login_WithCorrectPassword_Succeeds()
    {
        (await LoginAsync(ApiFixture.TestUserName, ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithWrongPassword_IsRejected()
    {
        (await LoginAsync(ApiFixture.TestUserName, ApiFixture.TestPassword + "x")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownUser_IsRejected()
    {
        (await LoginAsync("nobody.here", ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_InactiveUser_IsRejected_EvenWithTheRightPassword()
    {
        await SetStatusAsync(_fixture.TestUserId, UserStatus.Inactive);

        (await LoginAsync(ApiFixture.TestUserName, ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UserWithoutPassword_IsRejected()
    {
        await AddUserAsync("no.password", password: null);

        (await LoginAsync("no.password", "anything-at-all")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_StoredPlainTextPassword_NoLongerWorks()
    {
        // Pre-hashing data: the stored value is the password itself. It must not log anyone in.
        await AddUserAsync("legacy.user", password: "legacy-plain-password");

        (await LoginAsync("legacy.user", "legacy-plain-password")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_CorruptStoredHash_IsRejected()
    {
        await AddUserAsync("corrupt.user", password: "$argon2id$v=19$m=65536,t=3,p=1$not-valid");

        (await LoginAsync("corrupt.user", "whatever-password")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_PasswordLongerThanThePolicyAllows_IsRejected()
    {
        (await LoginAsync(ApiFixture.TestUserName, new string('a', PasswordPolicy.MaxLength + 1)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WhenAnInactiveAccountSharesTheUserName_TheActiveOneStillLogsIn()
    {
        // Active-only uniqueness lets a deactivated account keep a name a new account also uses.
        await AddUserAsync(ApiFixture.TestUserName, password: new PasswordHasher().Hash("old-account-password"), status: UserStatus.Inactive);

        (await LoginAsync(ApiFixture.TestUserName, ApiFixture.TestPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoginAsync(ApiFixture.TestUserName, "old-account-password")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UserNameMode_IsCaseSensitive_AndIgnoresSurroundingSpaces()
    {
        (await LoginAsync(ApiFixture.TestUserName.ToUpperInvariant(), ApiFixture.TestPassword))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginAsync($"  {ApiFixture.TestUserName} ", ApiFixture.TestPassword))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_EmailMode_UserNameIsMatchedCaseInsensitively()
    {
        await AddUserAsync("Mixed.Case@Example.test", password: new PasswordHasher().Hash(GoodPassword));
        await using var factory = _fixture.WithWebHostBuilder(b => b.UseSetting("Auth:EmailMode", "UniqueEmail"));

        var response = await LoginAsync("mixed.case@EXAMPLE.test", GoodPassword, factory.CreateClient());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_AfterRegistrationAndActivation_WorksWithTheNewPassword_NotBefore()
    {
        var client = _fixture.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new { UserName = "fresh.user", Email = "fresh.user@example.test", DisplayName = "Fresh" });
        var pending = await _fixture.QueryDbAsync(db =>
            db.Set<User>().AsNoTracking().SingleAsync(u => u.StatusId == UserStatus.JustCreated));

        // Pending: neither the plain nor the stored (prefixed) name can log in, whatever the password.
        (await LoginAsync("fresh.user", GoodPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginAsync(pending.UserName, GoodPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var key = _fixture.ResetKeySender.LatestKeyFor(pending.Id)!;
        (await client.PostAsJsonAsync("/api/auth/reset-password", new { ResetKey = key, NewPassword = GoodPassword }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await LoginAsync("fresh.user", GoodPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoginAsync("fresh.user", GoodPassword + "x")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- equal time ----------------------------------------------------------------------

    [Fact]
    public async Task Login_EveryFailureTakesAboutAsLongAsAWrongPassword()
    {
        await AddUserAsync("inactive.user", password: new PasswordHasher().Hash(GoodPassword), status: UserStatus.Inactive);
        await AddUserAsync("no.password", password: null);
        await AddUserAsync("legacy.user", password: "legacy-plain-password");
        await AddUserAsync("corrupt.user", password: "$argon2id$v=19$m=65536,t=3,p=1$not-valid");

        // Reference: an Active user with the wrong password — a real Argon2 verification.
        var reference = await MedianLoginMillisecondsAsync(ApiFixture.TestUserName, "wrong-password-1");
        reference.ShouldBeGreaterThan(20, "Argon2 should dominate a login; is hashing still wired in?");

        var cases = new (string Name, string User, string Password)[]
        {
            ("unknown user", "nobody.here", "wrong-password-1"),
            ("inactive user, right password", "inactive.user", GoodPassword),
            ("user without password", "no.password", "wrong-password-1"),
            ("plain-text stored value", "legacy.user", "legacy-plain-password"),
            ("corrupt stored hash", "corrupt.user", "wrong-password-1"),
            ("over-long password", ApiFixture.TestUserName, new string('a', PasswordPolicy.MaxLength + 50)),
        };
        foreach (var (name, user, password) in cases)
        {
            var elapsed = await MedianLoginMillisecondsAsync(user, password);
            // A skipped Argon2 run would be a few ms against ~100+; the bounds are loose on purpose.
            elapsed.ShouldBeGreaterThan(reference * 0.5, $"{name}: {elapsed:F0} ms vs wrong password {reference:F0} ms — failed too fast");
            elapsed.ShouldBeLessThan(reference * 3, $"{name}: {elapsed:F0} ms vs wrong password {reference:F0} ms — failed too slowly");
        }
    }

    // ---- PasswordHasher ------------------------------------------------------------------

    [Fact]
    public void VerifyEqualTime_OnlyAcceptsTheRightPasswordAgainstAUsableHash()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash(GoodPassword);

        hasher.VerifyEqualTime(hash, GoodPassword).ShouldBeTrue();
        hasher.VerifyEqualTime(hash, GoodPassword + "x").ShouldBeFalse();
        hasher.VerifyEqualTime(null, GoodPassword).ShouldBeFalse();
        hasher.VerifyEqualTime("", GoodPassword).ShouldBeFalse();
        hasher.VerifyEqualTime(GoodPassword, GoodPassword).ShouldBeFalse(); // plain text stored
        hasher.VerifyEqualTime("$argon2id$garbage", GoodPassword).ShouldBeFalse();
        hasher.VerifyEqualTime("$argon2i$v=19$m=16,t=2,p=1$c29tZXNhbHQ$aGFzaA", GoodPassword).ShouldBeFalse(); // other variant
        hasher.VerifyEqualTime(hash, new string('a', PasswordPolicy.MaxLength + 1)).ShouldBeFalse();
    }

    [Fact]
    public void VerifyEqualTime_DoesOneArgon2RunWhateverTheOutcome()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash(GoodPassword);
        var inputs = new (string? Hash, string Password)[]
        {
            (hash, GoodPassword),
            (hash, "wrong-password-1"),
            (null, "wrong-password-1"),
            ("legacy-plain", "legacy-plain"),
            ("$argon2id$garbage", "wrong-password-1"),
            ("$argon2id$v=19$m=65536,t=3,p=1$not-valid", "wrong-password-1"),
            ("$argon2id$v=19$m=65536,t=3,p=1$c29tZXNhbHQ$!!notbase64!!", "wrong-password-1"),
        };

        var reference = Median(() => hasher.VerifyEqualTime(hash, "wrong-password-1"));
        foreach (var (storedHash, password) in inputs)
        {
            var elapsed = Median(() => hasher.VerifyEqualTime(storedHash, password));
            elapsed.ShouldBeGreaterThan(reference * 0.5, $"{storedHash ?? "null"}: {elapsed:F0} ms vs {reference:F0} ms");
            elapsed.ShouldBeLessThan(reference * 3, $"{storedHash ?? "null"}: {elapsed:F0} ms vs {reference:F0} ms");
        }
    }

    // ---- helpers -------------------------------------------------------------------------

    private Task<HttpResponseMessage> LoginAsync(string userName, string password, HttpClient? client = null) =>
        (client ?? _fixture.CreateClient()).PostAsJsonAsync("/api/auth/login", new { UserName = userName, Password = password });

    private async Task<double> MedianLoginMillisecondsAsync(string userName, string password)
    {
        var client = _fixture.CreateClient();
        await LoginAsync(userName, password, client); // warm-up: JIT, connection, plan caches
        var samples = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew();
            var response = await LoginAsync(userName, password, client);
            watch.Stop();
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        return samples[samples.Count / 2];
    }

    private static double Median(Action action)
    {
        action();
        var samples = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew();
            action();
            watch.Stop();
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        return samples[samples.Count / 2];
    }

    private Task AddUserAsync(string userName, string? password, short status = UserStatus.Active) =>
        _fixture.QueryDbAsync(async db =>
        {
            db.Add(new User
            {
                UserName = userName,
                DisplayName = userName,
                FullName = userName,
                Password = password,
                StatusId = status,
            });
            await db.SaveChangesAsync();
            return 0;
        });

    private Task SetStatusAsync(int userId, short statusId) =>
        _fixture.QueryDbAsync(async db =>
        {
            await db.Set<User>().Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.StatusId, statusId));
            return 0;
        });
}
