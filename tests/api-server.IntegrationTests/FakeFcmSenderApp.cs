using System.Net.Http.Headers;
using exs.notifications_contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace api_server.IntegrationTests;

/// <summary>
/// Stands in for the standalone FCM sender on a real local port, speaking the wire contract
/// (POST /v1/messages, GET /v1/results) and nothing else. The host under test talks to it over real HTTP
/// exactly as it would to exs.sender-fcm, so this proves the host's side: what it submits, the key it
/// sends, and what it does with the results it pulls. The sender's own behaviour is covered in
/// server-side-senders and by the host-and-sender end-to-end tests in server-side-notifications.
/// </summary>
public sealed class FakeFcmSenderApp : IAsyncDisposable
{
    public const string ApiKey = "integration-test-sender-key";

    private readonly WebApplication _app;
    private readonly object _gate = new();
    private readonly List<MessageEnvelope<FcmBody>> _messages = [];
    private readonly List<ResultEvent> _results = [];
    private long _lastResultId;

    public string BaseUrl { get; }

    /// <summary>Tokens the fake FCM answers "unregistered" for; every other token is delivered.</summary>
    public HashSet<string> DeadTokens { get; } = [];

    public FakeFcmSenderApp()
    {
        var builder = WebApplication.CreateSlimBuilder();
        _app = builder.Build();
        _app.Urls.Add("http://127.0.0.1:0");

        _app.MapPost("/v1/messages", async (HttpContext http) =>
        {
            if (!authorised(http)) return Results.Unauthorized();

            using var reader = new StreamReader(http.Request.Body);
            var envelope = ContractJson.Deserialize<MessageEnvelope<FcmBody>>(await reader.ReadToEndAsync());
            if (envelope is null) return Results.BadRequest();

            lock (_gate)
            {
                var duplicate = _messages.Any(m => m.MessageId == envelope.MessageId);
                if (!duplicate)
                {
                    _messages.Add(envelope);
                    _results.Add(resultFor(envelope, ++_lastResultId));
                }
                return Results.Json(
                    new IntakeResponse { MessageId = envelope.MessageId, Status = duplicate ? IntakeStatuses.Duplicate : IntakeStatuses.Accepted },
                    ContractJson.Options,
                    statusCode: StatusCodes.Status202Accepted);
            }
        });

        _app.MapGet("/v1/results", (HttpContext http, long after = 0, int limit = 100) =>
        {
            if (!authorised(http)) return Results.Unauthorized();

            lock (_gate)
            {
                var page = _results.Where(r => r.ResultId > after).Take(limit).ToList();
                return Results.Json(
                    new ResultPage { Results = page, NextAfter = page.Count == 0 ? after : page[^1].ResultId },
                    ContractJson.Options);
            }
        });

        _app.StartAsync().GetAwaiter().GetResult();
        BaseUrl = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
    }

    /// <summary>Every distinct message accepted so far, in arrival order.</summary>
    public IReadOnlyList<MessageEnvelope<FcmBody>> Messages
    {
        get { lock (_gate) return _messages.ToList(); }
    }

    /// <summary>
    /// Forgets what was received so far, between tests. Result ids keep counting up rather than restarting, so a
    /// host that remembers its read position never mistakes a new result for one it has already seen.
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _messages.Clear();
            _results.Clear();
            DeadTokens.Clear();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private static bool authorised(HttpContext http) =>
        AuthenticationHeaderValue.TryParse(http.Request.Headers.Authorization, out var header)
        && header.Scheme == "Bearer"
        && header.Parameter == ApiKey;

    private ResultEvent resultFor(MessageEnvelope<FcmBody> envelope, long resultId)
    {
        var token = envelope.Body.Destination.Token;
        var dead = DeadTokens.Contains(token);
        return new ResultEvent
        {
            SchemaVersion = envelope.SchemaVersion,
            ResultId = resultId,
            MessageId = envelope.MessageId,
            Channel = envelope.Channel,
            Account = envelope.Account,
            Ref = envelope.Ref,
            Status = dead ? ResultStatuses.Failed : ResultStatuses.Sent,
            Destination = token,
            DestinationInvalid = dead,
            ErrorKind = dead ? ErrorKinds.DestinationInvalid : null,
            ErrorCode = dead ? "Unregistered" : null,
            ProviderMessageId = dead ? null : $"fake-{resultId}",
            Attempts = 1,
            CompletedAt = DateTimeOffset.UtcNow,
        };
    }
}
