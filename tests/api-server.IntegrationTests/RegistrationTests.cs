using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using db_model.UserManagement;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class RegistrationTests : IAsyncLifetime
{
    private const string GoodPassword = "Correct-horse-battery-9";

    // "{32 hex chars}_" in front of the real value.
    private static string PendingPattern(string value) => $"^[0-9a-f]{{32}}_{Regex.Escape(value)}$";

    private readonly ApiFixture _fixture;

    public RegistrationTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ---- user name mode (default): creation ----------------------------------------------

    [Fact]
    public async Task Register_UserNameMode_CreatesPendingUser_WithPrefixedUserName_AndIssuesKey()
    {
        var response = await RegisterAsync(_fixture.CreateClient(), new
        {
            UserName = "new.person",
            Email = "new.person@example.test",
            DisplayName = "New Person",
            FullName = "Newly Registered Person",
            Phone = "+1 (813) 555-0142",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var user = (await GetPendingAsync("new.person")).ShouldHaveSingleItem();
        Regex.IsMatch(user.UserName, PendingPattern("new.person")).ShouldBeTrue(user.UserName);
        user.Email.ShouldBe("new.person@example.test"); // only the unique field is prefixed
        user.StatusId.ShouldBe(UserStatus.JustCreated);
        user.UserSourceId.ShouldBe(UserSource.Registered);
        user.Password.ShouldBeNull();
        user.DisplayName.ShouldBe("New Person");
        user.FullName.ShouldBe("Newly Registered Person");
        user.Phone.ShouldBe("+1 (813) 555-0142");

        _fixture.ResetKeySender.HasKeyFor(user.Id).ShouldBeTrue();
        _fixture.ResetKeySender.RecipientFor(user.Id).ShouldBe("new.person@example.test");
        (await CountKeysAsync(user.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Register_OptionalFieldsOmitted_FullNameFallsBackToDisplayName()
    {
        var response = await RegisterAsync(_fixture.CreateClient(), new
        {
            UserName = "minimal",
            Email = "minimal@example.test",
            DisplayName = "Minimal",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var user = (await GetPendingAsync("minimal")).ShouldHaveSingleItem();
        user.FullName.ShouldBe("Minimal");
        user.Phone.ShouldBeNull();
    }

    [Fact]
    public async Task Register_SameUserNameTwice_CreatesTwoPendingUsers_EachWithItsOwnKey()
    {
        var client = _fixture.CreateClient();

        var first = await RegisterAsync(client, new { UserName = "popular", Email = "a@example.test", DisplayName = "A" });
        var second = await RegisterAsync(client, new { UserName = "popular", Email = "b@example.test", DisplayName = "B" });

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var users = await GetPendingAsync("popular");
        users.Count.ShouldBe(2);
        users.Select(u => u.UserName).Distinct().Count().ShouldBe(2); // different GUID prefixes
        foreach (var user in users)
        {
            (await CountKeysAsync(user.Id)).ShouldBe(1);
        }
    }

    [Fact]
    public async Task Register_UserNameMode_UserNameHeldByActiveUser_Returns409()
    {
        var response = await RegisterAsync(_fixture.CreateClient(), new
        {
            UserName = ApiFixture.TestUserName,
            Email = "someone.else@example.test",
            DisplayName = "Someone Else",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("user name already exists");
        (await CountUsersAsync()).ShouldBe(1);
    }

    [Theory]
    [InlineData(UserStatus.Inactive)]
    [InlineData(UserStatus.JustCreated)]
    public async Task Register_UserNameMode_UserNameHeldByNonActiveUser_IsAllowed(short status)
    {
        await SetStatusAsync(_fixture.TestUserId, status);

        var response = await RegisterAsync(_fixture.CreateClient(), new
        {
            UserName = ApiFixture.TestUserName,
            Email = "someone.else@example.test",
            DisplayName = "Someone Else",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountUsersAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Register_UserNameMode_WithoutUserName_Returns400()
    {
        var response = await RegisterAsync(_fixture.CreateClient(),
            new { Email = "no.name@example.test", DisplayName = "No Name" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("User name is required");
        (await CountUsersAsync()).ShouldBe(1); // just the seeded user
    }

    [Fact]
    public async Task Register_UserNameMode_DuplicateEmailWithNewUserName_IsAllowed()
    {
        var response = await RegisterAsync(_fixture.CreateClient(), new
        {
            UserName = "second.account",
            Email = "test.user@example.test", // the seeded user's e-mail; only user names must be unique here
            DisplayName = "Second Account",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountUsersAsync()).ShouldBe(2);
    }

    // ---- unique e-mail mode: creation ----------------------------------------------------

    [Fact]
    public async Task Register_EmailMode_IgnoresUserName_AndPrefixesBothUserNameAndEmail()
    {
        await using var factory = EmailModeFactory();

        var response = await RegisterAsync(factory.CreateClient(), new
        {
            UserName = "ignored.name",
            Email = "new.person@example.test",
            DisplayName = "New Person",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetPendingAsync("ignored.name")).ShouldBeEmpty();
        var user = (await GetPendingAsync("new.person@example.test")).ShouldHaveSingleItem();
        // The e-mail is the login name, so both fields carry the same prefix.
        Regex.IsMatch(user.UserName, PendingPattern("new.person@example.test")).ShouldBeTrue(user.UserName);
        user.Email.ShouldBe(user.UserName);
        user.StatusId.ShouldBe(UserStatus.JustCreated);
        // ...but the key goes to the real address, not the stored one.
        _fixture.ResetKeySender.RecipientFor(user.Id).ShouldBe("new.person@example.test");
    }

    [Fact]
    public async Task Register_EmailMode_WithoutUserName_Succeeds()
    {
        await using var factory = EmailModeFactory();

        var response = await RegisterAsync(factory.CreateClient(),
            new { Email = "only.email@example.test", DisplayName = "Only Email" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetPendingAsync("only.email@example.test")).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Register_EmailMode_SameEmailTwice_CreatesTwoPendingUsers()
    {
        await using var factory = EmailModeFactory();
        var client = factory.CreateClient();

        (await RegisterAsync(client, new { Email = "twice@example.test", DisplayName = "One" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RegisterAsync(client, new { Email = "TWICE@example.test", DisplayName = "Two" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await CountUsersAsync()).ShouldBe(3);
    }

    [Theory]
    [InlineData("test.user@example.test")]
    [InlineData("  TEST.User@Example.TEST ")]
    public async Task Register_EmailMode_EmailHeldByActiveUser_Returns409(string email)
    {
        await using var factory = EmailModeFactory();

        var response = await RegisterAsync(factory.CreateClient(), new { Email = email, DisplayName = "Dup" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("e-mail already exists");
        (await CountUsersAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Register_EmailMode_EmailHeldByInactiveUser_IsAllowed()
    {
        await SetStatusAsync(_fixture.TestUserId, UserStatus.Inactive);
        await using var factory = EmailModeFactory();

        var response = await RegisterAsync(factory.CreateClient(),
            new { Email = "test.user@example.test", DisplayName = "Back Again" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // ---- pending users are not reachable through "forgot password" -----------------------

    [Fact]
    public async Task RequestPasswordReset_ForAPendingRegistration_FindsNobody()
    {
        await RegisterAsync(_fixture.CreateClient(),
            new { UserName = "pending.one", Email = "pending.one@example.test", DisplayName = "Pending" });
        var user = (await GetPendingAsync("pending.one")).Single();

        var response = await _fixture.CreateClient().PostAsJsonAsync("/api/auth/request-password-reset",
            new { Identifier = "pending.one" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _fixture.ResetKeySender.KeysFor(user.Id).Count.ShouldBe(1); // just the registration key
        (await CountKeysAsync(user.Id)).ShouldBe(1);
    }

    // ---- activation (setting the password) -----------------------------------------------

    [Fact]
    public async Task Activate_UserNameMode_RemovesPrefix_ActivatesUser_AndDeletesOtherRegistrationsOfTheName()
    {
        var client = _fixture.CreateClient();
        await RegisterAsync(client, new { UserName = "popular", Email = "a@example.test", DisplayName = "A" });
        await RegisterAsync(client, new { UserName = "popular", Email = "b@example.test", DisplayName = "B" });
        await RegisterAsync(client, new { UserName = "popular", Email = "c@example.test", DisplayName = "C" });
        var users = await GetPendingAsync("popular");
        var winner = users.Single(u => u.DisplayName == "B");
        var losers = users.Where(u => u.Id != winner.Id).ToList();

        var response = await ResetAsync(client, _fixture.ResetKeySender.LatestKeyFor(winner.Id)!, GoodPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var activated = (await GetUserByIdAsync(winner.Id))!;
        activated.UserName.ShouldBe("popular"); // prefix gone
        activated.Email.ShouldBe("b@example.test");
        activated.StatusId.ShouldBe(UserStatus.Active);
        activated.Password.ShouldStartWith("$argon2id$");
        foreach (var loser in losers)
        {
            (await GetUserByIdAsync(loser.Id)).ShouldBeNull();
            (await CountKeysAsync(loser.Id)).ShouldBe(0);
        }
        (await CountKeysAsync(winner.Id)).ShouldBe(0);
        (await CountUsersAsync()).ShouldBe(2); // seeded user + the winner

        // The losers' keys no longer work.
        foreach (var loser in losers)
        {
            (await ResetAsync(client, _fixture.ResetKeySender.LatestKeyFor(loser.Id)!, GoodPassword))
                .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task Activate_RemovesAllOfTheWinnersKeys_NotOnlyTheOneUsed()
    {
        await using var factory = _fixture.WithWebHostBuilder(b => b.UseSetting("Auth:PasswordResetCooldownSeconds", "0"));
        var client = factory.CreateClient();
        await RegisterAsync(client, new { UserName = "keyed", Email = "keyed@example.test", DisplayName = "Keyed" });
        var user = (await GetPendingAsync("keyed")).Single();
        // A pending user is not reachable through "forgot password", so give them extra keys directly.
        await _fixture.QueryDbAsync(async db =>
        {
            db.Add(new UserPasswordResetKey
            {
                KeyHash = new string('a', 64),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
            });
            await db.SaveChangesAsync();
            return 0;
        });
        (await CountKeysAsync(user.Id)).ShouldBe(2);

        (await ResetAsync(client, _fixture.ResetKeySender.LatestKeyFor(user.Id)!, GoodPassword))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await CountKeysAsync(user.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Activate_DoesNotTouchNamesThatMerelyEndTheSameWay()
    {
        var client = _fixture.CreateClient();
        await RegisterAsync(client, new { UserName = "bob", Email = "bob@example.test", DisplayName = "Bob" });
        await RegisterAsync(client, new { UserName = "alice_bob", Email = "alice@example.test", DisplayName = "Alice" });
        await RegisterAsync(client, new { UserName = "x_bob", Email = "x@example.test", DisplayName = "X" });
        await RegisterAsync(client, new { UserName = "BOB", Email = "upper@example.test", DisplayName = "Upper" });
        var bob = (await GetPendingAsync("bob")).Single(u => u.DisplayName == "Bob");

        (await ResetAsync(client, _fixture.ResetKeySender.LatestKeyFor(bob.Id)!, GoodPassword))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // "_bob" is a suffix of all of them, but only an exact "{guid}_bob" is the same registration.
        (await GetPendingAsync("alice_bob")).ShouldHaveSingleItem();
        (await GetPendingAsync("x_bob")).ShouldHaveSingleItem();
        (await GetPendingAsync("BOB")).ShouldHaveSingleItem(); // user names compare case-sensitively
        (await GetUserByIdAsync(bob.Id))!.UserName.ShouldBe("bob");
    }

    [Fact]
    public async Task Activate_LeavesOtherStatusesAndUnregisteredUsersAlone()
    {
        var client = _fixture.CreateClient();
        await RegisterAsync(client, new { UserName = "shared", Email = "one@example.test", DisplayName = "One" });
        await RegisterAsync(client, new { UserName = "shared", Email = "two@example.test", DisplayName = "Two" });
        var one = (await GetPendingAsync("shared")).Single(u => u.DisplayName == "One");
        var two = (await GetPendingAsync("shared")).Single(u => u.DisplayName == "Two");
        // "Two" is no longer a pending registration (e.g. an admin deactivated it): leave it be.
        await SetStatusAsync(two.Id, UserStatus.Inactive);

        (await ResetAsync(client, _fixture.ResetKeySender.LatestKeyFor(one.Id)!, GoodPassword))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetUserByIdAsync(two.Id)).ShouldNotBeNull();
        (await CountKeysAsync(two.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Activate_WhenAnActiveUserTookTheNameMeanwhile_Returns409_AndChangesNothing()
    {
        var client = _fixture.CreateClient();
        await RegisterAsync(client, new { UserName = "contested", Email = "late@example.test", DisplayName = "Late" });
        var late = (await GetPendingAsync("contested")).Single();
        await _fixture.QueryDbAsync(async db =>
        {
            db.Add(new User
            {
                UserName = "contested",
                DisplayName = "Early",
                FullName = "Early",
                StatusId = UserStatus.Active,
            });
            await db.SaveChangesAsync();
            return 0;
        });

        var response = await ResetAsync(client, _fixture.ResetKeySender.LatestKeyFor(late.Id)!, GoodPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var unchanged = (await GetUserByIdAsync(late.Id))!;
        unchanged.StatusId.ShouldBe(UserStatus.JustCreated);
        unchanged.UserName.ShouldBe(late.UserName);
        unchanged.Password.ShouldBeNull();
    }

    [Fact]
    public async Task Activate_WithWeakPassword_Returns400_AndLeavesTheRegistrationUntouched()
    {
        var client = _fixture.CreateClient();
        await RegisterAsync(client, new { UserName = "weak.one", Email = "weak.one@example.test", DisplayName = "Weak" });
        await RegisterAsync(client, new { UserName = "weak.one", Email = "weak.two@example.test", DisplayName = "Weak 2" });
        var first = (await GetPendingAsync("weak.one")).First();

        var response = await ResetAsync(client, _fixture.ResetKeySender.LatestKeyFor(first.Id)!, "short");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var unchanged = (await GetUserByIdAsync(first.Id))!;
        unchanged.UserName.ShouldBe(first.UserName);
        unchanged.StatusId.ShouldBe(UserStatus.JustCreated);
        (await CountUsersAsync()).ShouldBe(3); // nobody was deleted
    }

    [Fact]
    public async Task Activate_PasswordRulesSeeTheRealUserName_NotThePrefixedOne()
    {
        var client = _fixture.CreateClient();
        await RegisterAsync(client, new { UserName = "mailer.guy", Email = "someone@example.test", DisplayName = "Mailer" });
        var user = (await GetPendingAsync("mailer.guy")).Single();

        var response = await ResetAsync(client, _fixture.ResetKeySender.LatestKeyFor(user.Id)!, "my-mailer.guy-pass");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("user name or e-mail");
    }

    [Fact]
    public async Task Activate_ExpiredRegistrationKey_Returns400_AndNothingChanges()
    {
        var client = _fixture.CreateClient();
        await RegisterAsync(client, new { UserName = "too.late", Email = "too.late@example.test", DisplayName = "Late" });
        var user = (await GetPendingAsync("too.late")).Single();
        await _fixture.QueryDbAsync(async db =>
        {
            await db.Set<UserPasswordResetKey>()
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
            return 0;
        });

        var response = await ResetAsync(client, _fixture.ResetKeySender.LatestKeyFor(user.Id)!, GoodPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var unchanged = (await GetUserByIdAsync(user.Id))!;
        unchanged.StatusId.ShouldBe(UserStatus.JustCreated);
        unchanged.UserName.ShouldBe(user.UserName);
    }

    [Fact]
    public async Task Activate_EmailMode_RemovesPrefixFromBothFields_AndDeletesTheOtherRegistrationsCaseInsensitively()
    {
        await using var factory = EmailModeFactory();
        var client = factory.CreateClient();
        await RegisterAsync(client, new { Email = "Flow@Example.test", DisplayName = "First" });
        await RegisterAsync(client, new { Email = "flow@example.test", DisplayName = "Second" });
        await RegisterAsync(client, new { Email = "other@example.test", DisplayName = "Other" });
        var pending = await _fixture.QueryDbAsync(db =>
            db.Set<User>().AsNoTracking().Where(u => u.StatusId == UserStatus.JustCreated).ToListAsync());
        var first = pending.Single(u => u.DisplayName == "First");
        var second = pending.Single(u => u.DisplayName == "Second");
        var other = pending.Single(u => u.DisplayName == "Other");

        var response = await ResetAsync(client, _fixture.ResetKeySender.LatestKeyFor(second.Id)!, GoodPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var activated = (await GetUserByIdAsync(second.Id))!;
        activated.UserName.ShouldBe("flow@example.test");
        activated.Email.ShouldBe("flow@example.test");
        activated.StatusId.ShouldBe(UserStatus.Active);
        (await GetUserByIdAsync(first.Id)).ShouldBeNull();
        (await CountKeysAsync(first.Id)).ShouldBe(0);
        (await GetUserByIdAsync(other.Id)).ShouldNotBeNull(); // a different address
        (await CountKeysAsync(other.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Activate_EmailMode_WhenAnActiveUserHoldsTheEmail_Returns409()
    {
        await using var factory = EmailModeFactory();
        var client = factory.CreateClient();
        await RegisterAsync(client, new { Email = "contested@example.test", DisplayName = "Late" });
        var late = (await _fixture.QueryDbAsync(db =>
            db.Set<User>().AsNoTracking().Where(u => u.StatusId == UserStatus.JustCreated).ToListAsync())).Single();
        await _fixture.QueryDbAsync(async db =>
        {
            db.Add(new User
            {
                UserName = "contested@example.test",
                Email = "contested@example.test",
                DisplayName = "Early",
                FullName = "Early",
                StatusId = UserStatus.Active,
            });
            await db.SaveChangesAsync();
            return 0;
        });

        var response = await ResetAsync(client, _fixture.ResetKeySender.LatestKeyFor(late.Id)!, GoodPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await GetUserByIdAsync(late.Id))!.StatusId.ShouldBe(UserStatus.JustCreated);
    }

    // ---- validation (both modes) ---------------------------------------------------------

    [Theory]
    [InlineData(null, "Valid Name", "E-mail is required")]
    [InlineData("", "Valid Name", "E-mail is required")]
    [InlineData("not-an-email", "Valid Name", "not a valid address")]
    [InlineData("a@b", "Valid Name", "not a valid address")]
    [InlineData("good@example.test", null, "Display name is required")]
    [InlineData("good@example.test", "   ", "Display name is required")]
    public async Task Register_MissingOrInvalidRequiredFields_Returns400(string? email, string? displayName, string expected)
    {
        var response = await RegisterAsync(_fixture.CreateClient(),
            new { UserName = "valid.user", Email = email, DisplayName = displayName });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain(expected);
        (await CountUsersAsync()).ShouldBe(1);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("call-me-maybe")]
    [InlineData("123456789012345678901")]
    public async Task Register_InvalidPhone_Returns400(string phone)
    {
        var response = await RegisterAsync(_fixture.CreateClient(), new
        {
            UserName = "phone.user",
            Email = "phone.user@example.test",
            DisplayName = "Phone User",
            Phone = phone,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Phone");
        (await GetPendingAsync("phone.user")).ShouldBeEmpty();
    }

    // ---- activation key lifetime ---------------------------------------------------------

    [Fact]
    public async Task Register_KeyLivesOneDayByDefault_UnlikeResetKeys()
    {
        await RegisterAsync(_fixture.CreateClient(),
            new { UserName = "slow.reader", Email = "slow.reader@example.test", DisplayName = "Slow Reader" });
        var user = (await GetPendingAsync("slow.reader")).Single();

        var record = await _fixture.QueryDbAsync(db =>
            db.Set<UserPasswordResetKey>().AsNoTracking().SingleAsync(r => r.UserId == user.Id));

        (record.ExpiresAt - record.CreatedAt).ShouldBe(TimeSpan.FromDays(1), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Register_KeyLifetimeComesFromConfiguration()
    {
        await using var factory = _fixture.WithWebHostBuilder(b => b.UseSetting("Auth:RegistrationKeyMinutes", "90"));

        await RegisterAsync(factory.CreateClient(),
            new { UserName = "configured", Email = "configured@example.test", DisplayName = "Configured" });
        var user = (await GetPendingAsync("configured")).Single();

        var record = await _fixture.QueryDbAsync(db =>
            db.Set<UserPasswordResetKey>().AsNoTracking().SingleAsync(r => r.UserId == user.Id));
        (record.ExpiresAt - record.CreatedAt).ShouldBe(TimeSpan.FromMinutes(90), TimeSpan.FromSeconds(1));
    }

    // ---- atomicity -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WhenStoringTheKeyFails_LeavesNoUserBehind()
    {
        await ExecuteSqlAsync(
            """
            CREATE FUNCTION fail_reset_key_insert() RETURNS trigger AS $$
            BEGIN RAISE EXCEPTION 'simulated failure'; END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER fail_reset_key BEFORE INSERT ON user_password_reset_key
                FOR EACH ROW EXECUTE FUNCTION fail_reset_key_insert();
            """);
        try
        {
            var response = await RegisterAsync(_fixture.CreateClient(),
                new { UserName = "doomed", Email = "doomed@example.test", DisplayName = "Doomed" });

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            (await GetPendingAsync("doomed")).ShouldBeEmpty();
            (await CountUsersAsync()).ShouldBe(1); // just the seeded user
        }
        finally
        {
            await ExecuteSqlAsync(
                """
                DROP TRIGGER fail_reset_key ON user_password_reset_key;
                DROP FUNCTION fail_reset_key_insert();
                """);
        }
    }

    // ---- rate limiting -------------------------------------------------------------------

    [Fact]
    public async Task Register_HasItsOwnRateLimit_ThatDoesNotConsumeTheLoginBudget()
    {
        await using var factory = _fixture.WithWebHostBuilder(b => b.UseSetting("RateLimiting:Register:PermitLimit", "2"));
        var client = factory.CreateClient();

        (await RegisterAsync(client, new { UserName = "a.one", Email = "a.one@example.test", DisplayName = "A" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RegisterAsync(client, new { UserName = "a.two", Email = "a.two@example.test", DisplayName = "A" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RegisterAsync(client, new { UserName = "a.three", Email = "a.three@example.test", DisplayName = "A" }))
            .StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PasswordResetRequest_AndResetPassword_HaveSeparateRateLimits()
    {
        await using var factory = _fixture.WithWebHostBuilder(b =>
            b.UseSetting("RateLimiting:PasswordResetRequest:PermitLimit", "2"));
        var client = factory.CreateClient();

        for (var i = 0; i < 2; i++)
        {
            (await client.PostAsJsonAsync("/api/auth/request-password-reset", new { Identifier = "nobody" }))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
        (await client.PostAsJsonAsync("/api/auth/request-password-reset", new { Identifier = "nobody" }))
            .StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // Exhausting the request bucket must not block completing a reset.
        (await ResetAsync(client, "1.wrong", GoodPassword)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- helpers -------------------------------------------------------------------------

    private WebApplicationFactory<Program> EmailModeFactory() =>
        _fixture.WithWebHostBuilder(b => b.UseSetting("Auth:EmailMode", "UniqueEmail"));

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, object body) =>
        client.PostAsJsonAsync("/api/auth/register", body);

    private static Task<HttpResponseMessage> ResetAsync(HttpClient client, string resetKey, string newPassword) =>
        client.PostAsJsonAsync("/api/auth/reset-password", new { ResetKey = resetKey, NewPassword = newPassword });

    /// <summary>JustCreated registrations whose unique field is exactly "{guid}_{value}".</summary>
    private async Task<List<User>> GetPendingAsync(string value)
    {
        var candidates = await _fixture.QueryDbAsync(db =>
            db.Set<User>().AsNoTracking().Where(u => u.StatusId == UserStatus.JustCreated).ToListAsync());
        return candidates.Where(u => Regex.IsMatch(u.UserName, PendingPattern(value))).ToList();
    }

    private Task<User?> GetUserByIdAsync(int id) =>
        _fixture.QueryDbAsync(db => db.Set<User>().AsNoTracking().SingleOrDefaultAsync(u => u.Id == id));

    private Task<int> CountUsersAsync() => _fixture.QueryDbAsync(db => db.Set<User>().CountAsync());

    private Task<int> CountKeysAsync(int userId) =>
        _fixture.QueryDbAsync(db => db.Set<UserPasswordResetKey>().CountAsync(r => r.UserId == userId));

    private Task SetStatusAsync(int userId, short statusId) =>
        _fixture.QueryDbAsync(async db =>
        {
            await db.Set<User>().Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.StatusId, statusId));
            return 0;
        });

    private Task ExecuteSqlAsync(string sql) =>
        _fixture.QueryDbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync(sql);
            return 0;
        });
}
