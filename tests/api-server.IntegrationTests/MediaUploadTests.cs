using System.Net;
using System.Net.Http.Json;
using db_model.Media;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class MediaUploadTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;

    public MediaUploadTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static readonly byte[] FileBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]; // PNG magic

    [Fact]
    public async Task Upload_ValidImage_CreatesDbRowAndStoresFile()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var guid = Guid.NewGuid().ToString();

        var response = await client.PostAsync("/api/media/upload", MultipartUpload(MediaType.Image, guid));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<MediaResponse>();
        dto!.Id.ShouldBeGreaterThan(0);
        dto.MediaType.ShouldBe(MediaType.Image);
        // The endpoint returns a ready-to-use absolute URL, not the bare filename.
        dto.Url.ShouldNotBeNull();
        dto.Url.ShouldStartWith("http://media.test/");
        // Stored name is the client-supplied Guid plus an extension fixed by MediaType
        // (not the uploaded file's own name/extension).
        dto.Url.ShouldEndWith($"{guid}.png");

        var record = await _fixture.QueryDbAsync(db =>
            db.Set<UploadedMedia>().AsNoTracking().SingleAsync(m => m.Id == dto.Id));
        record.MediaType.ShouldBe(MediaType.Image);
        record.FileName.ShouldBe($"{guid}.png");

        // The bytes must land on disk under the configured upload folder, unaltered.
        var storedPath = Path.Combine(_fixture.MediaUploadFolder, record.FileName!);
        File.Exists(storedPath).ShouldBeTrue();
        (await File.ReadAllBytesAsync(storedPath)).ShouldBe(FileBytes);
    }

    [Fact]
    public async Task Upload_DistinctGuids_GetDistinctStoredFiles()
    {
        // Storage is keyed by the client-supplied Guid, so two uploads with the same
        // OriginalFileName still land as distinct files as long as their Guids differ.
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var first = await client.PostAsync("/api/media/upload", MultipartUpload(MediaType.Image));
        var second = await client.PostAsync("/api/media/upload", MultipartUpload(MediaType.Image));

        var firstDto = await first.Content.ReadFromJsonAsync<MediaResponse>();
        var secondDto = await second.Content.ReadFromJsonAsync<MediaResponse>();
        secondDto!.Url.ShouldNotBe(firstDto!.Url);

        Directory.GetFiles(_fixture.MediaUploadFolder, "*.png").Length.ShouldBe(2);
    }

    [Fact]
    public async Task Upload_EmptyFile_Returns400()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent([]), "File", "empty.png" },
            { new StringContent("empty.png"), "OriginalFileName" },
            { new StringContent(MediaType.Image.ToString()), "MediaType" },
            { new StringContent(Guid.NewGuid().ToString()), "Guid" },
        };

        var response = await client.PostAsync("/api/media/upload", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_WithoutFile_Returns400()
    {
        // UploadRequest.File is non-nullable, so implicit NRT validation rejects
        // a form that has the metadata fields but no file part.
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var content = new MultipartFormDataContent
        {
            { new StringContent("avatar.png"), "OriginalFileName" },
            { new StringContent(MediaType.Image.ToString()), "MediaType" },
            { new StringContent(Guid.NewGuid().ToString()), "Guid" },
        };

        var response = await client.PostAsync("/api/media/upload", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_EmptyGuid_Returns400()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(FileBytes), "File", "avatar.png" },
            { new StringContent("avatar.png"), "OriginalFileName" },
            { new StringContent(MediaType.Image.ToString()), "MediaType" },
            { new StringContent(""), "Guid" },
        };

        var response = await client.PostAsync("/api/media/upload", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_ArbitraryOriginalFileName_DoesNotAffectStoredPath()
    {
        // OriginalFileName no longer feeds the stored path at all — storage is keyed
        // purely by Guid plus a MediaType-derived extension — so even a path-traversal-
        // shaped OriginalFileName must not escape the upload folder or affect storage.
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var guid = Guid.NewGuid().ToString();
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(FileBytes), "File", "x.png" },
            { new StringContent(@"..\..\evil.png"), "OriginalFileName" },
            { new StringContent(MediaType.Image.ToString()), "MediaType" },
            { new StringContent(guid), "Guid" },
        };

        var response = await client.PostAsync("/api/media/upload", content);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<MediaResponse>();
        dto!.Url.ShouldNotContain("..");

        var record = await _fixture.QueryDbAsync(db =>
            db.Set<UploadedMedia>().AsNoTracking().SingleAsync(m => m.Id == dto.Id));
        record.FileName.ShouldBe($"{guid}.png");

        // The stored file must live directly inside the upload folder.
        var storedPath = Path.GetFullPath(Path.Combine(_fixture.MediaUploadFolder, record.FileName!));
        Path.GetDirectoryName(storedPath).ShouldBe(Path.GetFullPath(_fixture.MediaUploadFolder));
        File.Exists(storedPath).ShouldBeTrue();
    }

    [Fact]
    public async Task Upload_WithoutToken_Returns401()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsync("/api/media/upload", MultipartUpload(MediaType.Image));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static MultipartFormDataContent MultipartUpload(byte mediaType, string? guid = null) =>
        new()
        {
            { new ByteArrayContent(FileBytes), "File", "avatar.png" },
            { new StringContent("avatar.png"), "OriginalFileName" },
            { new StringContent(mediaType.ToString()), "MediaType" },
            // Each call is a distinct logical upload and needs its own Guid — reusing one
            // across calls hits MediaStorageService.SaveAsync's existing-Guid short-circuit.
            { new StringContent(guid ?? Guid.NewGuid().ToString()), "Guid" },
        };

    private sealed record MediaResponse(int Id, string? Url, string? PreviewUrl, byte MediaType);
}
