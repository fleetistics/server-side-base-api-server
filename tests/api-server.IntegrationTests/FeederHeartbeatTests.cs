using System.Collections.Concurrent;
using System.Net;
using api_server.Services.Feeders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>The debouncing and failure handling of the feeder heartbeat, with a scripted transport and a fake clock. No database, no host.</summary>
public sealed class FeederHeartbeatTests : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private sealed class ScriptedTransport : IFeederHeartbeatTransport
    {
        public readonly ConcurrentQueue<int> Sent = new();
        public Func<int, HeartbeatResult> Behavior = _ => HeartbeatResult.Accepted;
        public TaskCompletionSource? Gate;

        public async Task<HeartbeatResult> SendAsync(int companyId, CancellationToken cancellationToken)
        {
            Sent.Enqueue(companyId);
            if (Gate != null) await Gate.Task.WaitAsync(cancellationToken);
            return Behavior(companyId);
        }
    }

    private sealed class CapturingLogger : ILogger<FeederHeartbeat>
    {
        public readonly ConcurrentQueue<(LogLevel Level, string Message)> Entries = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Entries.Enqueue((logLevel, formatter(state, exception)));
    }

    private readonly FakeTimeProvider mTime = new(T0);
    private readonly ScriptedTransport mTransport = new();
    private readonly CapturingLogger mLog = new();
    private FeederHeartbeat? mHeartbeat;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (mHeartbeat != null) await mHeartbeat.StopAsync(CancellationToken.None);
    }

    private static FeederHeartbeatOptions options(Action<FeederHeartbeatOptions>? tweak = null)
    {
        var o = new FeederHeartbeatOptions { BaseUrl = "http://127.0.0.1:5110", ApiKey = "the-key", MinIntervalSeconds = 10, FailureBackoffSeconds = 30, QueueCapacity = 16 };
        tweak?.Invoke(o);
        return o;
    }

    private async Task<FeederHeartbeat> startAsync(FeederHeartbeatOptions? o = null)
    {
        mHeartbeat = new FeederHeartbeat(Options.Create(o ?? options()), mTransport, mTime, mLog);
        await mHeartbeat.StartAsync(CancellationToken.None);
        return mHeartbeat;
    }

    private static async Task eventuallyAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(10);
        }
        throw new TimeoutException($"Timed out waiting for {what}.");
    }

    /// <summary>Gives the sender a moment to (not) act, for the tests that assert nothing happens.</summary>
    private static Task settleAsync() => Task.Delay(150);

    // ---- on and off ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Without_a_base_url_nothing_is_sent_and_the_log_says_why()
    {
        var heartbeat = await startAsync(options(o => { o.BaseUrl = null; o.ApiKey = null; }));

        heartbeat.CompanyWatched(5);
        await settleAsync();

        heartbeat.Enabled.ShouldBeFalse();
        mTransport.Sent.ShouldBeEmpty();
        mLog.Entries.ShouldContain(e => e.Level == LogLevel.Information && e.Message.Contains("BaseUrl is not set"));
    }

    [Theory]
    [InlineData("not a url", "key", 10)]
    [InlineData("ftp://host/", "key", 10)]
    [InlineData("http://127.0.0.1:5110", null, 10)]
    [InlineData("http://127.0.0.1:5110", "  ", 10)]
    [InlineData("http://127.0.0.1:5110", "key", 0)]
    [InlineData("http://127.0.0.1:5110", "key", 31)]
    public async Task A_bad_configuration_turns_heartbeats_off_with_an_error_instead_of_breaking_the_api(string baseUrl, string? key, int minInterval)
    {
        var heartbeat = await startAsync(options(o => { o.BaseUrl = baseUrl; o.ApiKey = key; o.MinIntervalSeconds = minInterval; }));

        heartbeat.CompanyWatched(5);
        await settleAsync();

        heartbeat.Enabled.ShouldBeFalse();
        mTransport.Sent.ShouldBeEmpty();
        mLog.Entries.ShouldContain(e => e.Level == LogLevel.Error && e.Message.Contains("Feeders:Heartbeat"));
    }

    [Fact]
    public void The_default_settings_are_valid_and_a_missing_url_is_simply_off()
    {
        options().Validate().ShouldBeEmpty();
        new FeederHeartbeatOptions().Validate().ShouldBeEmpty(); // nothing configured: nothing to complain about
        new FeederHeartbeatOptions().IsConfigured.ShouldBeFalse();
    }

    // ---- debouncing ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_first_read_of_a_company_sends_at_once_and_repeats_within_the_interval_do_not()
    {
        var heartbeat = await startAsync();

        heartbeat.CompanyWatched(5);
        await eventuallyAsync(() => mTransport.Sent.Count == 1, "the first heartbeat");
        for (var i = 0; i < 50; i++)
        {
            mTime.Advance(TimeSpan.FromMilliseconds(100)); // five seconds of map reads
            heartbeat.CompanyWatched(5);
        }
        await settleAsync();

        mTransport.Sent.ToArray().ShouldBe([5]);
    }

    [Fact]
    public async Task After_the_interval_the_next_read_sends_again()
    {
        var heartbeat = await startAsync();
        heartbeat.CompanyWatched(5);
        await eventuallyAsync(() => mTransport.Sent.Count == 1, "the first heartbeat");

        mTime.Advance(TimeSpan.FromSeconds(10));
        heartbeat.CompanyWatched(5);

        await eventuallyAsync(() => mTransport.Sent.Count == 2, "the second heartbeat");
    }

    [Fact]
    public async Task Companies_are_debounced_separately()
    {
        var heartbeat = await startAsync();

        heartbeat.CompanyWatched(5);
        heartbeat.CompanyWatched(6);
        heartbeat.CompanyWatched(5);
        heartbeat.CompanyWatched(7);

        await eventuallyAsync(() => mTransport.Sent.Count == 3, "three heartbeats");
        mTransport.Sent.OrderBy(x => x).ToArray().ShouldBe([5, 6, 7]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task An_invalid_company_id_is_ignored(int companyId)
    {
        var heartbeat = await startAsync();

        heartbeat.CompanyWatched(companyId);
        await settleAsync();

        mTransport.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Companies_nobody_watches_any_more_are_forgotten()
    {
        var heartbeat = await startAsync(options(o => o.QueueCapacity = 5000));
        for (var id = 1; id <= 1500; id++) heartbeat.CompanyWatched(id);
        await eventuallyAsync(() => mTransport.Sent.Count == 1500, "all heartbeats to be sent");
        heartbeat.TrackedCompanyCount.ShouldBeGreaterThan(1024);

        mTime.Advance(TimeSpan.FromMinutes(5));
        heartbeat.CompanyWatched(9999);

        heartbeat.TrackedCompanyCount.ShouldBeLessThan(10); // the old ones would not suppress anything any more
    }

    // ---- failures -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task When_the_feeder_is_down_the_api_stops_trying_for_the_backoff_and_tries_again_after_it()
    {
        mTransport.Behavior = _ => HeartbeatResult.Unavailable;
        var heartbeat = await startAsync();
        heartbeat.CompanyWatched(5);
        await eventuallyAsync(() => mTransport.Sent.Count == 1, "the failing heartbeat");
        await eventuallyAsync(() => mLog.Entries.Any(e => e.Level == LogLevel.Warning), "the warning");

        mTime.Advance(TimeSpan.FromSeconds(15));
        heartbeat.CompanyWatched(5);
        heartbeat.CompanyWatched(6);
        await settleAsync();
        mTransport.Sent.Count.ShouldBe(1); // inside the 30 s backoff: nothing is attempted, nobody waits on a dead feeder

        mTime.Advance(TimeSpan.FromSeconds(16));
        mTransport.Behavior = _ => HeartbeatResult.Accepted;
        heartbeat.CompanyWatched(5);

        await eventuallyAsync(() => mTransport.Sent.Count == 2, "the retry after the backoff");
        await eventuallyAsync(() => mLog.Entries.Any(e => e.Message.Contains("accepted again")), "the recovery message");
    }

    [Fact]
    public async Task A_refused_key_backs_off_the_same_way_and_is_logged_as_an_error_once()
    {
        mTransport.Behavior = _ => HeartbeatResult.Rejected;
        var heartbeat = await startAsync();

        heartbeat.CompanyWatched(5);
        await eventuallyAsync(() => mLog.Entries.Any(e => e.Level == LogLevel.Error && e.Message.Contains("rejected this API's key")), "the error");
        mTime.Advance(TimeSpan.FromSeconds(31));
        heartbeat.CompanyWatched(5);
        await eventuallyAsync(() => mTransport.Sent.Count == 2, "the retry");
        await settleAsync();

        mLog.Entries.Count(e => e.Level == LogLevel.Error && e.Message.Contains("rejected this API's key")).ShouldBe(1); // not once per attempt
        mLog.Entries.ToArray().Any(e => e.Message.Contains("the-key")).ShouldBeFalse(); // the key is never logged
    }

    [Fact]
    public async Task A_refusal_for_one_company_does_not_stop_the_others()
    {
        mTransport.Behavior = id => id == 5 ? HeartbeatResult.Invalid : HeartbeatResult.Accepted;
        var heartbeat = await startAsync();

        heartbeat.CompanyWatched(5);
        heartbeat.CompanyWatched(6);

        await eventuallyAsync(() => mTransport.Sent.Count == 2, "both heartbeats");
        mTime.Advance(TimeSpan.FromSeconds(1));
        heartbeat.CompanyWatched(7);
        await eventuallyAsync(() => mTransport.Sent.Count == 3, "a third company right after");
    }

    [Fact]
    public async Task A_transport_that_throws_is_treated_as_unavailable_and_never_stops_the_sender()
    {
        mTransport.Behavior = id => id == 5 ? throw new InvalidOperationException("boom") : HeartbeatResult.Accepted;
        var heartbeat = await startAsync();
        heartbeat.CompanyWatched(5);
        await eventuallyAsync(() => mTransport.Sent.Count == 1, "the throwing heartbeat");
        await eventuallyAsync(() => mLog.Entries.Any(e => e.Level == LogLevel.Warning), "the failure to be recorded");

        mTime.Advance(TimeSpan.FromSeconds(31));
        heartbeat.CompanyWatched(6);

        await eventuallyAsync(() => mTransport.Sent.Count == 2, "the next heartbeat after the backoff");
    }

    // ---- never on the request path ------------------------------------------------------------------------------

    [Fact]
    public async Task A_stuck_feeder_never_blocks_the_caller_and_a_full_queue_drops_instead_of_waiting()
    {
        mTransport.Gate = new TaskCompletionSource(); // the first send hangs for ever
        var heartbeat = await startAsync(options(o => o.QueueCapacity = 2));

        var started = DateTime.UtcNow;
        for (var id = 1; id <= 100; id++) heartbeat.CompanyWatched(id);

        (DateTime.UtcNow - started).ShouldBeLessThan(TimeSpan.FromSeconds(1));
        await eventuallyAsync(() => mTransport.Sent.Count == 1, "the stuck send");
        heartbeat.TrackedCompanyCount.ShouldBeLessThanOrEqualTo(3); // one in flight + two queued; the rest were dropped, not remembered
    }

    [Fact]
    public async Task A_company_dropped_because_the_queue_was_full_is_tried_again_on_its_next_read()
    {
        mTransport.Gate = new TaskCompletionSource();
        var heartbeat = await startAsync(options(o => o.QueueCapacity = 1));
        heartbeat.CompanyWatched(1);
        await eventuallyAsync(() => mTransport.Sent.Count == 1, "the stuck send");
        heartbeat.CompanyWatched(2); // queued
        heartbeat.CompanyWatched(3); // dropped: the queue holds one

        mTransport.Gate.SetResult();
        await eventuallyAsync(() => mTransport.Sent.Count == 2, "the queued heartbeat");
        heartbeat.CompanyWatched(3);

        await eventuallyAsync(() => mTransport.Sent.Contains(3), "the dropped company's retry");
    }

    [Fact]
    public async Task Stopping_while_a_send_hangs_still_completes()
    {
        mTransport.Gate = new TaskCompletionSource();
        var heartbeat = await startAsync();
        heartbeat.CompanyWatched(5);
        await eventuallyAsync(() => mTransport.Sent.Count == 1, "the stuck send");

        await heartbeat.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Calling_from_many_threads_is_safe_and_sends_each_company_once()
    {
        var heartbeat = await startAsync(options(o => o.QueueCapacity = 1000));

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var id = 1; id <= 200; id++) heartbeat.CompanyWatched(id);
        })));

        await eventuallyAsync(() => mTransport.Sent.Count >= 200, "all companies");
        await settleAsync();
        mTransport.Sent.Count.ShouldBe(200);
        mTransport.Sent.Distinct().Count().ShouldBe(200);
    }
}

/// <summary>The HTTP request the heartbeat makes, against a stub handler.</summary>
public sealed class HttpFeederHeartbeatTransportTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Requests = new();
        public Func<HttpRequestMessage, HttpResponseMessage> Respond = _ => new HttpResponseMessage(HttpStatusCode.Accepted);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Respond(request));
        }
    }

    private static (HttpFeederHeartbeatTransport Transport, Handler Handler) transport(string baseUrl = "http://feeder.internal:5110", string key = "the-key")
    {
        var handler = new Handler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<FeederHeartbeatOptions>(o => { o.BaseUrl = baseUrl; o.ApiKey = key; });
        services.AddHttpClient(HttpFeederHeartbeatTransport.HttpClientName).ConfigureHttpClient((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<FeederHeartbeatOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl!.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
        }).ConfigurePrimaryHttpMessageHandler(() => handler);
        var provider = services.BuildServiceProvider();
        return (new HttpFeederHeartbeatTransport(provider.GetRequiredService<IHttpClientFactory>()), handler);
    }

    [Fact]
    public async Task It_posts_to_the_companys_live_path_with_the_bearer_key()
    {
        var (transport, handler) = HttpFeederHeartbeatTransportTests.transport();

        var result = await transport.SendAsync(42, CancellationToken.None);

        result.ShouldBe(HeartbeatResult.Accepted);
        var request = handler.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.ToString().ShouldBe("http://feeder.internal:5110/v1/companies/42/live");
        request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe("the-key");
    }

    [Theory]
    [InlineData("http://feeder.internal:5110/")]
    [InlineData("http://feeder.internal:5110")]
    public async Task A_trailing_slash_in_the_address_makes_no_difference(string baseUrl)
    {
        var (transport, handler) = HttpFeederHeartbeatTransportTests.transport(baseUrl);

        await transport.SendAsync(7, CancellationToken.None);

        handler.Requests.Single().RequestUri!.AbsolutePath.ShouldBe("/v1/companies/7/live");
    }

    [Fact]
    public async Task A_feeder_behind_a_path_prefix_is_reached_under_it()
    {
        var (transport, handler) = HttpFeederHeartbeatTransportTests.transport("http://gateway.internal/feeders/geotab");

        await transport.SendAsync(7, CancellationToken.None);

        handler.Requests.Single().RequestUri!.AbsolutePath.ShouldBe("/feeders/geotab/v1/companies/7/live");
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted, HeartbeatResult.Accepted)]
    [InlineData(HttpStatusCode.OK, HeartbeatResult.Accepted)]
    [InlineData(HttpStatusCode.Unauthorized, HeartbeatResult.Rejected)]
    [InlineData(HttpStatusCode.Forbidden, HeartbeatResult.Rejected)]
    [InlineData(HttpStatusCode.BadRequest, HeartbeatResult.Invalid)]
    [InlineData(HttpStatusCode.NotFound, HeartbeatResult.Invalid)]
    [InlineData(HttpStatusCode.TooManyRequests, HeartbeatResult.Unavailable)]
    [InlineData(HttpStatusCode.InternalServerError, HeartbeatResult.Unavailable)]
    [InlineData(HttpStatusCode.BadGateway, HeartbeatResult.Unavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, HeartbeatResult.Unavailable)]
    public async Task The_answer_is_sorted_by_what_the_caller_should_do(HttpStatusCode status, HeartbeatResult expected)
    {
        var (transport, handler) = HttpFeederHeartbeatTransportTests.transport();
        handler.Respond = _ => new HttpResponseMessage(status);

        (await transport.SendAsync(1, CancellationToken.None)).ShouldBe(expected);
    }

    [Fact]
    public async Task A_refused_connection_a_timeout_and_a_dropped_connection_are_unavailable_not_exceptions()
    {
        var (transport, handler) = HttpFeederHeartbeatTransportTests.transport();

        handler.Respond = _ => throw new HttpRequestException("connection refused");
        (await transport.SendAsync(1, CancellationToken.None)).ShouldBe(HeartbeatResult.Unavailable);

        handler.Respond = _ => throw new TaskCanceledException("timed out");
        (await transport.SendAsync(1, CancellationToken.None)).ShouldBe(HeartbeatResult.Unavailable);

        handler.Respond = _ => throw new IOException("reset");
        (await transport.SendAsync(1, CancellationToken.None)).ShouldBe(HeartbeatResult.Unavailable);
    }

    [Fact]
    public async Task Shutting_down_is_not_swallowed()
    {
        var (transport, handler) = HttpFeederHeartbeatTransportTests.transport();
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        handler.Respond = _ => throw new OperationCanceledException(cancel.Token);

        await Should.ThrowAsync<OperationCanceledException>(() => transport.SendAsync(1, cancel.Token));
    }
}

/// <summary>The real registration against a real in-process feeder: what a host with the setting configured actually sends.</summary>
public sealed class FeederHeartbeatEndToEndTests : IAsyncLifetime
{
    private WebApplication mFeeder = null!;
    private readonly ConcurrentQueue<(string Path, string? Authorization)> mReceived = new();
    private ServiceProvider? mProvider;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        mFeeder = builder.Build();
        mFeeder.MapPost("/v1/companies/{companyId:int}/live", (int companyId, HttpContext http) =>
        {
            mReceived.Enqueue((http.Request.Path.Value!, http.Request.Headers.Authorization.ToString()));
            return Results.Accepted(value: new { companyId, becameLive = true });
        });
        await mFeeder.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (mProvider != null) await mProvider.DisposeAsync();
        await mFeeder.DisposeAsync();
    }

    private async Task<IFeederHeartbeat> hostAsync(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFeederHeartbeat(configuration);
        // The only change from production: the HTTP handler is the in-process feeder instead of the network.
        services.AddHttpClient(HttpFeederHeartbeatTransport.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => mFeeder.GetTestServer().CreateHandler());
        mProvider = services.BuildServiceProvider();
        foreach (var hosted in mProvider.GetServices<IHostedService>()) await hosted.StartAsync(CancellationToken.None);
        return mProvider.GetRequiredService<IFeederHeartbeat>();
    }

    private static async Task eventuallyAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(10);
        }
        throw new TimeoutException($"Timed out waiting for {what}.");
    }

    [Fact]
    public async Task A_configured_host_sends_the_authenticated_post_and_debounces_the_rest()
    {
        var heartbeat = await hostAsync(new()
        {
            ["Feeders:Heartbeat:BaseUrl"] = "http://feeder.internal:5110",
            ["Feeders:Heartbeat:ApiKey"] = "secret-key",
        });

        for (var i = 0; i < 20; i++) heartbeat.CompanyWatched(12);
        heartbeat.CompanyWatched(13);

        await eventuallyAsync(() => mReceived.Count == 2, "both heartbeats at the feeder");
        await Task.Delay(150);
        mReceived.Count.ShouldBe(2); // twenty reads of company 12 were one heartbeat
        mReceived.ShouldContain(r => r.Path == "/v1/companies/12/live" && r.Authorization == "Bearer secret-key");
        mReceived.ShouldContain(r => r.Path == "/v1/companies/13/live");
    }

    [Fact]
    public async Task An_unconfigured_host_registers_the_service_and_sends_nothing()
    {
        var heartbeat = await hostAsync(new());

        heartbeat.CompanyWatched(12);
        await Task.Delay(200);

        heartbeat.ShouldBeOfType<FeederHeartbeat>().Enabled.ShouldBeFalse();
        mReceived.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_settings_are_read_from_the_section_with_their_defaults()
    {
        var heartbeat = await hostAsync(new()
        {
            ["Feeders:Heartbeat:BaseUrl"] = "http://feeder.internal:5110",
            ["Feeders:Heartbeat:ApiKey"] = "k",
            ["Feeders:Heartbeat:MinIntervalSeconds"] = "99", // out of range: heartbeats must be off, not wrong
        });

        heartbeat.ShouldBeOfType<FeederHeartbeat>().Enabled.ShouldBeFalse();
        var defaults = mProvider!.GetRequiredService<IOptions<FeederHeartbeatOptions>>().Value;
        defaults.TimeoutSeconds.ShouldBe(3);
        defaults.FailureBackoffSeconds.ShouldBe(30);
    }
}
