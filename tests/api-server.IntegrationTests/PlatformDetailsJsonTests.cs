using System.Text.Json.Nodes;
using api_server.Auth;
using Shouldly;

namespace api_server.IntegrationTests;

public sealed class PlatformDetailsJsonTests
{
    [Fact]
    public void AddsClientIp_ToClientJson_KeepingOtherFields()
    {
        var result = PlatformDetailsJson.WithClientIp("""{"os":"Android","version":14}""", "203.0.113.7");

        var node = JsonNode.Parse(result)!.AsObject();
        node["os"]!.GetValue<string>().ShouldBe("Android");
        node["version"]!.GetValue<int>().ShouldBe(14);
        node[PlatformDetailsJson.ClientIpProperty]!.GetValue<string>().ShouldBe("203.0.113.7");
    }

    [Fact]
    public void OverwritesClientIp_SentByClient()
    {
        var result = PlatformDetailsJson.WithClientIp("""{"ClientIp":"1.2.3.4"}""", "203.0.113.7");

        JsonNode.Parse(result)![PlatformDetailsJson.ClientIpProperty]!.GetValue<string>().ShouldBe("203.0.113.7");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyDetails_GiveObjectWithOnlyIp(string? details)
    {
        var node = JsonNode.Parse(PlatformDetailsJson.WithClientIp(details, "::1"))!.AsObject();

        node.Count.ShouldBe(1);
        node[PlatformDetailsJson.ClientIpProperty]!.GetValue<string>().ShouldBe("::1");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void NonObjectDetails_AreKeptUnderRaw_SoResultIsValidJson(string details)
    {
        var node = JsonNode.Parse(PlatformDetailsJson.WithClientIp(details, "::1"))!.AsObject();

        node["Raw"]!.GetValue<string>().ShouldBe(details);
        node[PlatformDetailsJson.ClientIpProperty]!.GetValue<string>().ShouldBe("::1");
    }

    [Fact]
    public void NoIp_ReturnsInputUnchanged()
    {
        PlatformDetailsJson.WithClientIp("""{"os":"iOS"}""", null).ShouldBe("""{"os":"iOS"}""");
        PlatformDetailsJson.WithClientIp(null, null).ShouldBe(string.Empty);
    }
}
