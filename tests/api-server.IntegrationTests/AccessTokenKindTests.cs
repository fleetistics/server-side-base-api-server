using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using api_server.Auth;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// The two kinds of access token are kept apart by audience and authentication scheme: an API token is refused by
/// the location report, a location-report token by every other endpoint. Tokens are minted directly so the test
/// depends only on the schemes, not on any login flow.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AccessTokenKindTests : IAsyncLifetime
{
    private const string ApiEndpoint = "/api/users/me";
    private const string LocationReportEndpoint = "/api/mobile-gps/location-report";
    private const string AnyDeviceId = "1";

    private readonly ApiFixture _fixture;

    public AccessTokenKindTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private HttpClient ClientWith(AccessTokenKind kind)
    {
        var tokens = _fixture.Services.GetRequiredService<TokenService>();
        // Both kinds carry the device claim, so a refusal can only come from the kind, never a missing claim.
        var token = tokens.CreateAccessToken(_fixture.TestUserId.ToString(), "1", AnyDeviceId, kind: kind);
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // An empty batch is accepted and queues nothing, so the call needs no device rows.
    private static Task<HttpResponseMessage> PostEmptyLocationReport(HttpClient client) =>
        client.PostAsJsonAsync(LocationReportEndpoint, Array.Empty<object>());

    [Fact]
    public async Task An_api_token_is_accepted_by_api_endpoints()
    {
        var response = await ClientWith(AccessTokenKind.Api).GetAsync(ApiEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_api_token_is_refused_by_the_location_report()
    {
        var response = await PostEmptyLocationReport(ClientWith(AccessTokenKind.Api));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_location_report_token_is_accepted_by_the_location_report()
    {
        var response = await PostEmptyLocationReport(ClientWith(AccessTokenKind.LocationReport));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_location_report_token_is_refused_by_api_endpoints()
    {
        var response = await ClientWith(AccessTokenKind.LocationReport).GetAsync(ApiEndpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
