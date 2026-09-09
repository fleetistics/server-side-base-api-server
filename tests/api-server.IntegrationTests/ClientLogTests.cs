using System.IO.Compression;
using System.Net;
using System.Text;
using db_model.Log;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ClientLogTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;

    public ClientLogTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private const string Payload =
        "{\"meta\":{\"trigger\":\"user\",\"comment\":\"it broke\"},\"entries\":[{\"level\":\"error\",\"message\":\"boom\"}]}";

    [Fact]
    public async Task Upload_PlainJson_StoresRecordForCurrentUser()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync("/api/client-log",
            new StringContent(Payload, Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var record = await _fixture.QueryDbAsync(db =>
            db.Set<ClientLogRecord>().AsNoTracking().SingleAsync());
        record.UserId.ShouldBe(_fixture.TestUserId);
        record.Log.ShouldBe(Payload);
    }

    [Fact]
    public async Task Upload_GzippedJson_IsDecompressedBeforeStoring()
    {
        // Mirrors what the SPA flight recorder sends: gzipped JSON tagged application/gzip.
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var content = new ByteArrayContent(Gzip(Payload));
        content.Headers.ContentType = new("application/gzip");

        var response = await client.PostAsync("/api/client-log", content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var record = await _fixture.QueryDbAsync(db =>
            db.Set<ClientLogRecord>().AsNoTracking().SingleAsync());
        record.Log.ShouldBe(Payload); // stored decompressed, not the gzip bytes
    }

    [Fact]
    public async Task Upload_WithoutToken_Returns401()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsync("/api/client-log",
            new StringContent(Payload, Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static byte[] Gzip(string text)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress))
        {
            gzip.Write(Encoding.UTF8.GetBytes(text));
        }
        return output.ToArray();
    }
}
