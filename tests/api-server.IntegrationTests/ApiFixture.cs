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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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

        // The schema is created through a throwaway host with its hosted services removed. The real host's
        // background services (log retention, media previews, the notification sweep) query their tables the
        // moment it starts, and the first one to fail stops the host - so the tables must exist before it starts.
        using (var schemaHost = WithWebHostBuilder(b => b.ConfigureTestServices(s => s.RemoveAll<IHostedService>())))
        using (var scope = schemaHost.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MainDatabaseContext>();
            await db.Database.EnsureCreatedAsync();
        }

        // LatestUpdate columns are ValueGeneratedOnAddOrUpdate: EF never sends a value and
        // expects the database to produce one. The production schema has defaults/triggers;
        // EnsureCreated does not generate them, so add the defaults here - for every table that has
        // the column, found from the schema rather than listed by hand (a list goes stale as soon as
        // a table is added or renamed, which is how this fixture once stopped starting at all).
        await ExecuteSqlAsync(
            """
            DO $$
            DECLARE t record;
            BEGIN
                FOR t IN SELECT table_name FROM information_schema.columns
                         WHERE table_schema = 'public' AND column_name = 'LatestUpdate'
                LOOP
                    EXECUTE format('ALTER TABLE %I ALTER COLUMN "LatestUpdate" SET DEFAULT now()', t.table_name);
                END LOOP;
            END $$;
            """);

        await using (var connection = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await connection.OpenAsync();
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
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

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
