using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using api_server.Auth;
using db_model.UserManagement;
using exs.modelCommons.UserManagement;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace api_server.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class PasswordResetTests : IAsyncLifetime
{
    private const string GoodPassword = "Correct-horse-battery-9";

    private readonly ApiFixture _fixture;

    public PasswordResetTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ---- request -------------------------------------------------------------------------

    [Fact]
    public async Task Request_ByUserName_StoresHashedKeyWithDefaultExpiry_AndDeliversKey()
    {
        var client = _fixture.CreateClient();

        var response = await RequestAsync(client, ApiFixture.TestUserName);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var key = _fixture.ResetKeySender.LatestKeyFor(_fixture.TestUserId);
        key.ShouldNotBeNullOrEmpty();

        var record = await GetRecordAsync(_fixture.TestUserId);
        record.ShouldNotBeNull();
        record.KeyHash.ShouldNotBe(key); // only a hash is persisted
        record.KeyHash.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key))));
        (record.ExpiresAt - record.CreatedAt).ShouldBe(TimeSpan.FromMinutes(60), TimeSpan.FromSeconds(1));
        record.CreatedAt.ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Request_ExpiryComesFromConfiguration()
    {
        await using var factory = _fixture.WithWebHostBuilder(b => b.UseSetting("Auth:PasswordResetKeyMinutes", "5"));

        var response = await RequestAsync(factory.CreateClient(), ApiFixture.TestUserName);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var record = await GetRecordAsync(_fixture.TestUserId);
        (record!.ExpiresAt - record.CreatedAt).ShouldBe(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Request_UnknownUser_ExitsSilently()
    {
        var response = await RequestAsync(_fixture.CreateClient(), "nobody.here");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountRecordsAsync()).ShouldBe(0);
        _fixture.ResetKeySender.HasKeyFor(_fixture.TestUserId).ShouldBeFalse();
    }

    [Fact]
    public async Task Request_WithoutAnyIdentifier_ExitsSilently()
    {
        var response = await RequestAsync(_fixture.CreateClient());

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountRecordsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Request_UserNameMode_IgnoresEmail()
    {
        var response = await RequestAsync(_fixture.CreateClient(), "test.user@example.test");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountRecordsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Request_EmailMode_FindsUserByEmailCaseInsensitively_AndIgnoresUserName()
    {
        await using var factory = EmailModeFactory();
        var client = factory.CreateClient();

        // User name alone is not enough in email mode...
        (await RequestAsync(client, ApiFixture.TestUserName)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountRecordsAsync()).ShouldBe(0);

        // ...the e-mail is what identifies the user, whatever the user name says.
        var response = await RequestAsync(client, " Test.User@Example.TEST ");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountRecordsAsync()).ShouldBe(1);
        _fixture.ResetKeySender.HasKeyFor(_fixture.TestUserId).ShouldBeTrue();
    }

    [Fact]
    public async Task Request_EmailMode_AmbiguousEmail_ExitsSilently()
    {
        await AddUserAsync("twin.one", "shared@example.test");
        await AddUserAsync("twin.two", "shared@example.test");
        await using var factory = EmailModeFactory();

        var response = await RequestAsync(factory.CreateClient(), "shared@example.test");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountRecordsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Request_InactiveUser_ExitsSilently()
    {
        await _fixture.QueryDbAsync(async db =>
        {
            await db.Set<User>().Where(u => u.Id == _fixture.TestUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.StatusId, UserStatus.Inactive));
            return 0;
        });

        var response = await RequestAsync(_fixture.CreateClient(), ApiFixture.TestUserName);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountRecordsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Reset_JustCreatedUserWithoutRegistrationPrefix_BecomesActive()
    {
        // An older JustCreated user (no "{guid}_" prefix) is simply activated.
        var client = _fixture.CreateClient();
        var key = await IssueKeyAsync(client);
        await SetStatusAsync(UserStatus.JustCreated);

        var response = await ResetAsync(client, key, GoodPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetStatusAsync()).ShouldBe(UserStatus.Active);
    }

    [Fact]
    public async Task Reset_RejectedPassword_KeepsJustCreatedStatus()
    {
        var client = _fixture.CreateClient();
        var key = await IssueKeyAsync(client);
        await SetStatusAsync(UserStatus.JustCreated);

        (await ResetAsync(client, key, "short"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await GetStatusAsync()).ShouldBe(UserStatus.JustCreated);
    }

    [Fact]
    public async Task Reset_ActiveUser_StaysActive()
    {
        var client = _fixture.CreateClient();
        var key = await IssueKeyAsync(client);

        (await ResetAsync(client, key, GoodPassword))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetStatusAsync()).ShouldBe(UserStatus.Active);
    }

    [Fact]
    public async Task Request_WithinCooldown_IsIgnored_KeepingTheIssuedKeyValid()
    {
        var client = _fixture.CreateClient();
        await RequestAsync(client, ApiFixture.TestUserName);
        var firstKey = _fixture.ResetKeySender.LatestKeyFor(_fixture.TestUserId)!;
        var firstRecord = (await GetRecordAsync(_fixture.TestUserId))!;

        // Someone else asking again right away must not invalidate the key just sent.
        var response = await RequestAsync(client, ApiFixture.TestUserName);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _fixture.ResetKeySender.LatestKeyFor(_fixture.TestUserId).ShouldBe(firstKey);
        (await GetRecordAsync(_fixture.TestUserId))!.KeyHash.ShouldBe(firstRecord.KeyHash);
        (await ResetAsync(client, firstKey, GoodPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Request_AfterCooldown_IssuesAnotherKey_AndTheEarlierOneStaysValid()
    {
        var client = _fixture.CreateClient();
        await RequestAsync(client, ApiFixture.TestUserName);
        var firstKey = _fixture.ResetKeySender.LatestKeyFor(_fixture.TestUserId)!;
        await _fixture.QueryDbAsync(async db =>
        {
            await db.Set<UserPasswordResetKey>()
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAt, DateTime.UtcNow.AddMinutes(-10)));
            return 0;
        });

        await RequestAsync(client, ApiFixture.TestUserName);
        var secondKey = _fixture.ResetKeySender.LatestKeyFor(_fixture.TestUserId)!;

        secondKey.ShouldNotBe(firstKey);
        (await CountRecordsAsync()).ShouldBe(2);
        // The earlier key was not replaced: it still works.
        (await ResetAsync(client, firstKey, GoodPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Request_IssuingAKey_DropsTheUsersExpiredKeys()
    {
        var client = _fixture.CreateClient();
        await RequestAsync(client, ApiFixture.TestUserName);
        await _fixture.QueryDbAsync(async db =>
        {
            await db.Set<UserPasswordResetKey>().ExecuteUpdateAsync(s => s
                .SetProperty(r => r.CreatedAt, DateTime.UtcNow.AddHours(-3))
                .SetProperty(r => r.ExpiresAt, DateTime.UtcNow.AddHours(-2)));
            return 0;
        });

        await RequestAsync(client, ApiFixture.TestUserName);

        (await CountRecordsAsync()).ShouldBe(1); // only the new one
    }

    [Fact]
    public async Task Request_SeveralKeys_AllWork_UntilOneSetsThePassword_ThenAllAreGone()
    {
        await using var factory = _fixture.WithWebHostBuilder(b => b.UseSetting("Auth:PasswordResetCooldownSeconds", "0"));
        var client = factory.CreateClient();

        await RequestAsync(client, ApiFixture.TestUserName);
        await RequestAsync(client, ApiFixture.TestUserName);
        await RequestAsync(client, ApiFixture.TestUserName);
        var keys = _fixture.ResetKeySender.KeysFor(_fixture.TestUserId);
        keys.Count.ShouldBe(3);
        keys.Distinct().Count().ShouldBe(3);
        (await CountRecordsAsync()).ShouldBe(3);

        // Any of them can be used — here the middle one...
        (await ResetAsync(client, keys[1], GoodPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // ...and setting the password of an Active user removes every key of that user.
        (await CountRecordsAsync()).ShouldBe(0);
        (await ResetAsync(client, keys[0], "Another-good-pass-1")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ResetAsync(client, keys[2], "Another-good-pass-1")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reset_ActiveUser_RemovesOnlyItsOwnKeys()
    {
        await AddUserAsync("bystander", "bystander@example.test");
        var bystanderId = await _fixture.QueryDbAsync(db =>
            db.Set<User>().Where(u => u.UserName == "bystander").Select(u => u.Id).SingleAsync());
        var client = _fixture.CreateClient();
        await RequestAsync(client, "bystander");
        var key = await IssueKeyAsync(client);

        (await ResetAsync(client, key, GoodPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetRecordAsync(bystanderId)).ShouldNotBeNull();
        (await GetRecordAsync(_fixture.TestUserId)).ShouldBeNull();
    }

    // ---- users without an e-mail address -------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Request_UserNameMode_UserWithoutEmail_StillGetsAKey(string? email)
    {
        await AddUserAsync("no.email", email);
        var userId = await _fixture.QueryDbAsync(db =>
            db.Set<User>().Where(u => u.UserName == "no.email").Select(u => u.Id).SingleAsync());

        var response = await RequestAsync(_fixture.CreateClient(), "no.email");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetRecordAsync(userId)).ShouldNotBeNull();
        var key = _fixture.ResetKeySender.LatestKeyFor(userId);
        key.ShouldStartWith($"{userId}."); // handed to the sender, which decides how to reach the user
        string.IsNullOrWhiteSpace(_fixture.ResetKeySender.RecipientFor(userId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Reset_WithAKeyIssuedToAUserWithoutEmail_Works()
    {
        await AddUserAsync("no.email", null);
        var userId = await _fixture.QueryDbAsync(db =>
            db.Set<User>().Where(u => u.UserName == "no.email").Select(u => u.Id).SingleAsync());
        var client = _fixture.CreateClient();
        await RequestAsync(client, "no.email");

        var response = await ResetAsync(client, _fixture.ResetKeySender.LatestKeyFor(userId)!, GoodPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // ---- minimum response time -----------------------------------------------------------

    [Fact]
    public async Task Request_TakesAtLeastTheMinimumResponseTime_WhateverHappens()
    {
        await using var factory = _fixture.WithWebHostBuilder(b =>
            b.UseSetting("Auth:PasswordResetMinResponseMilliseconds", "1000"));
        var client = factory.CreateClient();

        var unknown = await TimeRequestAsync(client, "nobody.here");
        var noIdentifier = await TimeRequestAsync(client, null);
        var issued = await TimeRequestAsync(client, ApiFixture.TestUserName);   // writes a key, "sends" it
        var cooldown = await TimeRequestAsync(client, ApiFixture.TestUserName); // ignored: key issued moments ago

        _fixture.ResetKeySender.HasKeyFor(_fixture.TestUserId).ShouldBeTrue(); // the real path did run
        foreach (var (name, elapsed) in new[] { ("unknown", unknown), ("no identifier", noIdentifier), ("issued", issued), ("cooldown", cooldown) })
        {
            elapsed.ShouldBeGreaterThanOrEqualTo(950, $"{name}: answered in {elapsed:F0} ms");
            elapsed.ShouldBeLessThan(3000, $"{name}: answered in {elapsed:F0} ms");
        }
    }

    [Fact]
    public async Task Request_WhenDeliveryFails_StillWaitsTheMinimumTime_BeforeFailing()
    {
        await using var factory = _fixture.WithWebHostBuilder(b =>
        {
            b.UseSetting("Auth:PasswordResetMinResponseMilliseconds", "1000");
            b.ConfigureServices(s => s.AddSingleton<IPasswordResetKeySender>(new ThrowingSender()));
        });
        var client = factory.CreateClient();

        var watch = Stopwatch.StartNew();
        var response = await RequestAsync(client, ApiFixture.TestUserName);
        watch.Stop();

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        watch.Elapsed.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(950);
    }

    [Fact]
    public async Task Request_WithTheMinimumDisabled_AnswersImmediately()
    {
        var elapsed = await TimeRequestAsync(_fixture.CreateClient(), "nobody.here"); // fixture default: 0

        elapsed.ShouldBeLessThan(800);
    }

    // ---- reset ---------------------------------------------------------------------------

    [Fact]
    public async Task Reset_ValidKey_StoresArgon2idHash_AndConsumesKey()
    {
        var client = _fixture.CreateClient();
        var key = await IssueKeyAsync(client);

        var response = await ResetAsync(client, key, GoodPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var stored = await GetPasswordAsync();
        stored.ShouldStartWith("$argon2id$");
        new PasswordHasher().Verify(stored!, GoodPassword).ShouldBeTrue();
        new PasswordHasher().Verify(stored!, GoodPassword + "x").ShouldBeFalse();
        (await GetRecordAsync(_fixture.TestUserId)).ShouldBeNull();

        // The key is single-use.
        (await ResetAsync(client, key, "Another-good-pass-1"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reset_ClosesAllActiveSessionsOfTheUser_AndNoOneElses()
    {
        const string otherPassword = "other-user-password";
        await AddUserAsync("other.user", "other.user@example.test", otherPassword);
        await LoginAsync(ApiFixture.TestUserName, ApiFixture.TestPassword);
        await LoginAsync(ApiFixture.TestUserName, ApiFixture.TestPassword);
        await LoginAsync("other.user", otherPassword);
        (await CountSessionsAsync(UserSessionStatus.Active)).ShouldBe(3);

        var client = _fixture.CreateClient();
        var key = await IssueKeyAsync(client);
        (await ResetAsync(client, key, GoodPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var sessions = await _fixture.QueryDbAsync(db =>
            db.Set<UserSession>().AsNoTracking().Select(s => new { s.UserId, s.StatusId }).ToListAsync());
        sessions.Where(s => s.UserId == _fixture.TestUserId).ShouldAllBe(s => s.StatusId == UserSessionStatus.LoggedOutByUser);
        sessions.Where(s => s.UserId != _fixture.TestUserId).ShouldAllBe(s => s.StatusId == UserSessionStatus.Active);
        sessions.Count(s => s.UserId == _fixture.TestUserId).ShouldBe(2);
    }

    [Fact]
    public async Task Reset_RejectedPassword_LeavesSessionsOpen()
    {
        await LoginAsync(ApiFixture.TestUserName, ApiFixture.TestPassword);
        var client = _fixture.CreateClient();
        var key = await IssueKeyAsync(client);

        (await ResetAsync(client, key, "short")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await CountSessionsAsync(UserSessionStatus.Active)).ShouldBe(1);
    }

    [Fact]
    public async Task Reset_EmailMode_KeyAloneIdentifiesTheUser()
    {
        await using var factory = EmailModeFactory();
        var client = factory.CreateClient();
        await RequestAsync(client, "test.user@example.test");
        var key = _fixture.ResetKeySender.LatestKeyFor(_fixture.TestUserId)!;

        (await ResetAsync(client, key, GoodPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetPasswordAsync()).ShouldStartWith("$argon2id$");
    }

    [Fact]
    public async Task Request_IssuesKeyCarryingTheUserId()
    {
        var key = await IssueKeyAsync(_fixture.CreateClient());

        key.ShouldStartWith($"{_fixture.TestUserId}.");
    }

    [Fact]
    public async Task Reset_WrongKey_Returns400_AndKeepsPasswordAndKey()
    {
        var client = _fixture.CreateClient();
        await IssueKeyAsync(client);

        var response = await ResetAsync(client, "not-the-key", GoodPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetPasswordAsync()).ShouldBe(ApiFixture.TestPasswordHash);
        (await GetRecordAsync(_fixture.TestUserId)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Reset_KeyWithOtherUsersId_Returns400()
    {
        var client = _fixture.CreateClient();
        var key = await IssueKeyAsync(client);
        var secret = key[(key.IndexOf('.') + 1)..];

        // Swapping the id for another user's must not let the secret work there.
        var response = await ResetAsync(client, $"{_fixture.TestUserId + 1}.{secret}", GoodPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetPasswordAsync()).ShouldBe(ApiFixture.TestPasswordHash);
        (await GetRecordAsync(_fixture.TestUserId)).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-dot-in-this-key")]
    [InlineData(".secret-only")]
    [InlineData("abc.not-a-numeric-id")]
    [InlineData("999999.unknown-user-secret")]
    public async Task Reset_MalformedOrUnknownKey_Returns400(string key)
    {
        var response = await ResetAsync(_fixture.CreateClient(), key, GoodPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reset_UserDeactivatedAfterKeyIssued_Returns400()
    {
        var client = _fixture.CreateClient();
        var key = await IssueKeyAsync(client);
        await SetStatusAsync(UserStatus.Inactive);

        var response = await ResetAsync(client, key, GoodPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetPasswordAsync()).ShouldBe(ApiFixture.TestPasswordHash);
    }

    [Fact]
    public async Task Reset_ExpiredKey_Returns400_AndRemovesKey()
    {
        var client = _fixture.CreateClient();
        var key = await IssueKeyAsync(client);
        await _fixture.QueryDbAsync(async db =>
        {
            await db.Set<UserPasswordResetKey>()
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
            return 0;
        });

        var response = await ResetAsync(client, key, GoodPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetPasswordAsync()).ShouldBe(ApiFixture.TestPasswordHash);
        (await GetRecordAsync(_fixture.TestUserId)).ShouldBeNull();
    }

    [Theory]
    [InlineData("short1!", "at least 8 characters")]
    [InlineData("password123", "too common")]
    [InlineData("aaaaaaaaaaaa", "single repeated character")]
    [InlineData("        ", "whitespace")]
    [InlineData("my-test.user-pass", "user name or e-mail")]
    public async Task Reset_WeakPassword_Returns400WithExplanation_AndKeepsKeyUsable(string weakPassword, string expectedExplanation)
    {
        var client = _fixture.CreateClient();
        var key = await IssueKeyAsync(client);

        var response = await ResetAsync(client, key, weakPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("NewPassword");
        body.ShouldContain(expectedExplanation);
        (await GetPasswordAsync()).ShouldBe(ApiFixture.TestPasswordHash);
        (await GetRecordAsync(_fixture.TestUserId)).ShouldNotBeNull();

        // A rejected password does not burn the key.
        (await ResetAsync(client, key, GoodPassword))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Reset_WeakPasswordWithWrongKey_DoesNotRevealRules()
    {
        var client = _fixture.CreateClient();
        await IssueKeyAsync(client);

        var response = await ResetAsync(client, "not-the-key", "short");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("characters");
    }

    // ---- helpers -------------------------------------------------------------------------

    private WebApplicationFactory<Program> EmailModeFactory() =>
        _fixture.WithWebHostBuilder(b => b.UseSetting("Auth:EmailMode", "UniqueEmail"));

    private static async Task<double> TimeRequestAsync(HttpClient client, string? identifier)
    {
        var watch = Stopwatch.StartNew();
        var response = await RequestAsync(client, identifier);
        watch.Stop();
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        return watch.Elapsed.TotalMilliseconds;
    }

    private sealed class ThrowingSender : IPasswordResetKeySender
    {
        public Task SendAsync(User user, string? recipientEmail, string resetKey, DateTime expiresAtUtc, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("mail server unreachable");
    }

    private static Task<HttpResponseMessage> RequestAsync(HttpClient client, string? identifier = null) =>
        client.PostAsJsonAsync("/api/auth/request-password-reset", new { Identifier = identifier });

    private static Task<HttpResponseMessage> ResetAsync(HttpClient client, string resetKey, string newPassword) =>
        client.PostAsJsonAsync("/api/auth/reset-password", new { ResetKey = resetKey, NewPassword = newPassword });

    private async Task<string> IssueKeyAsync(HttpClient client)
    {
        (await RequestAsync(client, ApiFixture.TestUserName)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        return _fixture.ResetKeySender.LatestKeyFor(_fixture.TestUserId)!;
    }

    private Task<UserPasswordResetKey?> GetRecordAsync(int userId) =>
        _fixture.QueryDbAsync(db =>
            db.Set<UserPasswordResetKey>().AsNoTracking().SingleOrDefaultAsync(r => r.UserId == userId));

    private Task<int> CountRecordsAsync() =>
        _fixture.QueryDbAsync(db => db.Set<UserPasswordResetKey>().CountAsync());

    private Task<string?> GetPasswordAsync() =>
        _fixture.QueryDbAsync(db =>
            db.Set<User>().AsNoTracking().Where(u => u.Id == _fixture.TestUserId).Select(u => u.Password).SingleAsync());

    private Task SetStatusAsync(short statusId) =>
        _fixture.QueryDbAsync(async db =>
        {
            await db.Set<User>().Where(u => u.Id == _fixture.TestUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.StatusId, statusId));
            return 0;
        });

    private Task<short> GetStatusAsync() =>
        _fixture.QueryDbAsync(db =>
            db.Set<User>().AsNoTracking().Where(u => u.Id == _fixture.TestUserId).Select(u => u.StatusId).SingleAsync());

    private async Task LoginAsync(string userName, string password)
    {
        var response = await _fixture.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { UserName = userName, Password = password });
        response.EnsureSuccessStatusCode();
    }

    private Task<int> CountSessionsAsync(short statusId) =>
        _fixture.QueryDbAsync(db => db.Set<UserSession>().CountAsync(s => s.StatusId == statusId));

    private Task AddUserAsync(string userName, string? email, string? password = null) =>
        _fixture.QueryDbAsync(async db =>
        {
            db.Add(new User
            {
                UserName = userName,
                DisplayName = userName,
                FullName = userName,
                Password = password is null ? null : new PasswordHasher().Hash(password),
                Email = email,
                StatusId = UserStatus.Active,
            });
            await db.SaveChangesAsync();
            return 0;
        });
}

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("1234567")]
    [InlineData("")]
    [InlineData("password")]
    [InlineData("Password1")]
    [InlineData("zzzzzzzz")]
    public void Validate_RejectsWeakPasswords(string password) =>
        PasswordPolicy.Validate(password).ShouldNotBeEmpty();

    [Theory]
    [InlineData("12345678 is not it")] // spaces and digits are fine; only the deny-list/length matter
    [InlineData("correct horse battery staple")]
    [InlineData("Pässwörd-ünïcode-✓")]
    public void Validate_AcceptsReasonablePasswords(string password) =>
        PasswordPolicy.Validate(password).ShouldBeEmpty();

    [Fact]
    public void Validate_EnforcesMaxLength() =>
        PasswordPolicy.Validate(new string('a', PasswordPolicy.MaxLength) + "b").ShouldContain(e => e.Contains("at most"));

    [Fact]
    public void Validate_RejectsPasswordContainingContextTerm() =>
        PasswordPolicy.Validate("xx-jdoe-yy-zz", "jdoe").ShouldContain(e => e.Contains("user name"));

    [Fact]
    public void Validate_IgnoresVeryShortContextTerms() =>
        PasswordPolicy.Validate("correct horse battery", "or").ShouldBeEmpty();
}

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash_ProducesSaltedArgon2idPhcString()
    {
        var hasher = new PasswordHasher();

        var first = hasher.Hash("Correct-horse-battery-9");
        var second = hasher.Hash("Correct-horse-battery-9");

        first.ShouldStartWith("$argon2id$v=19$m=65536,t=3,p=1$");
        first.ShouldNotBe(second); // random salt per hash
        hasher.Verify(first, "Correct-horse-battery-9").ShouldBeTrue();
        hasher.Verify(first, "wrong").ShouldBeFalse();
    }
}
