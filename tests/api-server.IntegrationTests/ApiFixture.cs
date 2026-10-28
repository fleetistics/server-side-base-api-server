using System.Net.Http.Headers;
using System.Net.Http.Json;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using mf.aiApi.mainDatabase.DatabaseContext;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace api_server.IntegrationTests;

/// <summary>
/// Boots the real application (WebApplicationFactory) against a real PostgreSQL
/// running in a disposable container. Shared by all test classes in the "api"
/// collection: the container and host start once; ResetDatabaseAsync() wipes and
/// re-seeds data between tests.
/// </summary>
public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestUserName = "test.user";
    public const string TestPassword = "integration-test-password";

    /// <summary>
    /// What the seeded user's Password column holds. Hashed once for the whole run (Argon2 is
    /// deliberately slow); tests compare against it to check "password unchanged".
    /// </summary>
    public static readonly string TestPasswordHash = new PasswordHasher().Hash(TestPassword);

    // The app registers Npgsql with UseNetTopologySuite(), so schema creation issues
    // CREATE EXTENSION postgis — the plain postgres image doesn't ship it.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:16-3.4-alpine")
        .Build();

    private Respawner? _respawner;

    public int TestUserId { get; private set; }

    /// <summary>Dedicated per-run folder MediaController writes uploads into.</summary>
    public string MediaUploadFolder { get; } =
        Path.Combine(Path.GetTempPath(), $"api-tests-media-{Guid.NewGuid():N}");

    /// <summary>Stands in for the e-mail channel so tests can read the reset keys the API issues.</summary>
    public CapturingPasswordResetKeySender ResetKeySender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IPasswordResetKeySender>(ResetKeySender);

            // The host starts before the schema exists (EnsureCreated below needs the host's
            // context), so hosted services that query on startup — NotificationService's sweep —
            // fail once. By default that stops and disposes the whole host; here it must not.
            services.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
        });

        // Not "Development": skips the dev-only static-files branch (which requires a
        // real Media:UploadFolder) and never loads appsettings.Development.json.
        builder.UseEnvironment("Testing");

        // Program.cs reads builder.Configuration while the WebApplicationBuilder is being
        // constructed — before WebApplicationFactory's ConfigureAppConfiguration overrides
        // are attached — and its top-level try/catch would swallow the resulting failure.
        // Process environment variables are read natively by WebApplication.CreateBuilder,
        // so they are always visible at that point.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__MainDatabase", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "integration-tests-signing-key-0123456789abcdef");
        Environment.SetEnvironmentVariable("Media__BaseUrl", "http://media.test");
        Environment.SetEnvironmentVariable("Media__UploadFolder", MediaUploadFolder);
        // Every test logs in; the brute-force limiter must not throttle the suite.
        Environment.SetEnvironmentVariable("RateLimiting__Login__PermitLimit", "100000");
        Environment.SetEnvironmentVariable("RateLimiting__Refresh__PermitLimit", "100000");

        // Every fixture starts from the unconfigured host, whatever fixture ran before it in this process.
        foreach (var name in NotificationEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
        ConfigureNotificationEnvironment();
    }

    /// <summary>
    /// The notification settings are read when the host is built, so they travel the same way as the rest:
    /// as process environment variables. The default is the unconfigured host (legacy queue pipeline, no email);
    /// a subclass overrides this to switch the transport. Fixtures must not run at the same time because
    /// they share the process environment (see xunit.runner.json).
    /// </summary>
    protected virtual void ConfigureNotificationEnvironment()
    {
    }

    protected static readonly string[] NotificationEnvironmentVariables =
    [
        "NotificationService__FcmTransport",
        "NotificationService__FcmSender__BaseUrl",
        "NotificationService__FcmSender__ApiKey",
        "NotificationService__FcmSender__RelayPollIntervalSeconds",
        "NotificationService__FcmSender__ResultPollIntervalSeconds",
        "NotificationService__FcmSender__AccountsByAppId__app-a",
        "NotificationService__EmailTransport",
        "NotificationService__EmailSender__BaseUrl",
        "NotificationService__EmailSender__ApiKey",
        "NotificationService__EmailSender__RelayPollIntervalSeconds",
        "NotificationService__EmailSender__ResultPollIntervalSeconds",
        // Opt-in /api/test/email endpoints: off unless a fixture turns them on.
        "TestEmail__Enabled",
    ];

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        // EnsureCreated still goes through LatestUpdateTriggerSqlGenerator, which emits a
        // CREATE TRIGGER referencing this function for every LatestUpdate column — but
        // EnsureCreated doesn't run migrations, so the function itself (normally created by
        // hand in InitialCreate) must be created here first.
        await ExecuteSqlAsync(
            """
            CREATE OR REPLACE FUNCTION update_latestupdate_column()
            RETURNS trigger AS $$
            BEGIN
                NEW."LatestUpdate" = now();
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            """);

        // Touching Services builds the host, which needs the container's connection string.
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MainDatabaseContext>();
            await db.Database.EnsureCreatedAsync();
        }

        // LatestUpdate columns are ValueGeneratedOnAddOrUpdate: EF never sends a value and
        // expects the database to produce one. The production schema has defaults/triggers;
        // EnsureCreated does not generate them, so add the defaults here.
        await ExecuteSqlAsync(
            """
            ALTER TABLE "user" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "user_status" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "user_session" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "uploaded_media" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "client_log_record" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "language" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "translation_token" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "translation" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "user_settings" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "team" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "team_details" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "team_member_user" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "team_uploaded_media" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "user_location_privacy" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "user_emergency_alert" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            ALTER TABLE "mobile_gps_device" ALTER COLUMN "LatestUpdate" SET DEFAULT now();
            """);

        await using (var connection = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await connection.OpenAsync();
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                // Model-seeded (HasData) rows that code and foreign keys depend on: wiping them would break every
                // test that creates a device with a TypeId.
                TablesToIgnore = [new Respawn.Graph.Table("gps_device_type")],
            });
        }

        await SeedAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        try
        {
            Directory.Delete(MediaUploadFolder, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // No test uploaded anything — nothing to clean.
        }
    }

    /// <summary>Wipes every table and re-seeds the standard test user. Call per test.</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
        await SeedAsync();
    }

    /// <summary>Runs an action against the application database, in its own scope.</summary>
    public async Task<T> QueryDbAsync<T>(Func<MainDatabaseContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDatabaseContext>();
        return await query(db);
    }

    /// <summary>Logs in as the seeded test user and returns a client with the bearer token set.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = TestUserName, Password = TestPassword });
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    private async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository>();
        var user = new User
        {
            UserName = TestUserName,
            // AuthService currently compares the stored value verbatim (see the TODO on
            // VerifyPassword) — when hashing lands, this seed changes with it.
            Password = TestPassword,
            DisplayName = "Test User",
            FullName = "Integration Test User",
            Email = "test.user@example.test",
            Phone = "8135550100",
            StatusId = UserStatus.Active,
        };
        repository.Create(user);
        await repository.SaveAsync(CancellationToken.None);
        TestUserId = user.Id;
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public sealed record LoginResponse(string AccessToken, int UserId, int SessionId, string? PreferredLanguage);
}

public sealed class CapturingPasswordResetKeySender : IPasswordResetKeySender
{
    private readonly ConcurrentDictionary<int, List<string>> _keys = new();
    private readonly ConcurrentDictionary<int, string?> _recipients = new();

    public Task SendAsync(User user, string? recipientEmail, string resetKey, DateTime expiresAtUtc, CancellationToken cancellationToken)
    {
        _keys.AddOrUpdate(user.Id, _ => [resetKey], (_, list) => { lock (list) { list.Add(resetKey); } return list; });
        _recipients[user.Id] = recipientEmail;
        return Task.CompletedTask;
    }

    public string? LatestKeyFor(int userId) => _keys.TryGetValue(userId, out var list) ? list[^1] : null;

    /// <summary>Every key sent to the user, oldest first.</summary>
    public IReadOnlyList<string> KeysFor(int userId) => _keys.TryGetValue(userId, out var list) ? [.. list] : [];

    public string? RecipientFor(int userId) => _recipients.GetValueOrDefault(userId);
    public bool HasKeyFor(int userId) => _keys.ContainsKey(userId);
    public void Clear() { _keys.Clear(); _recipients.Clear(); }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
