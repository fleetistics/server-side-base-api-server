using System.Net;
using Shouldly;

namespace api_server.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class HealthTests
{
    private readonly ApiFixture _fixture;

    public HealthTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Live_ReturnsHealthy()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Ready_WithDatabaseUp_ReturnsHealthy()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public async Task HealthEndpoints_AreAnonymous()
    {
        // Probes come from the orchestrator, which has no bearer token.
        var client = _fixture.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
