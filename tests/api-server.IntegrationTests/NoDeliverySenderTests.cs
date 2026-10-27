using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using api_server.Auth;
using db_model.UserManagement;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace api_server.IntegrationTests;

public sealed class NoDeliverySenderTests
{
    [Fact]
    public async Task TheKeyIsWrittenToTheLog_WithTheUserAndThePortalLinkPath()
    {
        var logger = new ListLogger();
        var sender = new NoDeliveryPasswordResetKeySender(logger);

        await sender.SendAsync(new User { Id = 42 }, "someone@example.test", "42.the-secret-part", new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("user 42");
        entry.Message.ShouldContain("Reset key: 42.the-secret-part");
        entry.Message.ShouldContain("/ResetPassword/42.the-secret-part");
        entry.Message.ShouldContain("has e-mail: True");
        entry.Message.ShouldNotContain("someone@example.test"); // the address itself stays out of the log
    }

    [Fact]
    public async Task AUserWithoutEmail_IsMarkedAsSuch()
    {
        var logger = new ListLogger();
        var sender = new NoDeliveryPasswordResetKeySender(logger);

        await sender.SendAsync(new User { Id = 7 }, null, "7.k", DateTime.UtcNow.AddHours(1), CancellationToken.None);

        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("has e-mail: False");
    }

    private sealed class ListLogger : ILogger<NoDeliveryPasswordResetKeySender>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}

/// <summary>The key read back out of the real log output is the exact key, and it works.</summary>
[Collection(ApiCollection.Name)]
public sealed class NoDeliverySenderLogTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;

    public NoDeliverySenderLogTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task TheKeyInTheRenderedLog_IsTheExactKey_AndSetsThePassword()
    {
        var sink = new CapturingLogSink();
        await using var factory = _fixture.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            // The real placeholder sender, instead of the fixture's capturing one.
            s.AddSingleton<IPasswordResetKeySender, NoDeliveryPasswordResetKeySender>();
            s.AddSingleton<Serilog.Core.ILogEventSink>(sink);
        }));
        var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/auth/request-password-reset", new { Identifier = ApiFixture.TestUserName }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var line = sink.Messages.Single(m => m.Contains("Reset key:"));
        var key = Regex.Match(line, @"Reset key: (\S+)").Groups[1].Value;
        key.ShouldStartWith($"{_fixture.TestUserId}.");
        key.ShouldNotContain("\""); // not wrapped in quotes by the log renderer
        line.ShouldContain($"/ResetPassword/{key}");

        (await client.PostAsJsonAsync("/api/auth/reset-password", new { ResetKey = key, NewPassword = "Correct-horse-battery-9" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private sealed class CapturingLogSink : Serilog.Core.ILogEventSink
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _messages = new();
        public IEnumerable<string> Messages => _messages;
        public void Emit(Serilog.Events.LogEvent logEvent) => _messages.Enqueue(logEvent.RenderMessage());
    }
}
