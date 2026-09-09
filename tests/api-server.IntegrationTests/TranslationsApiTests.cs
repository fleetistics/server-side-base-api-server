using System.Net;
using System.Net.Http.Json;
using db_model.Translations;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class TranslationsApiTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;

    public TranslationsApiTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetTranslations_English_WorksWithoutAuthAndReturnsKnownTexts()
    {
        var client = _fixture.CreateClient(); // no auth token — this must work anonymously
        await ReportAsync(client, "Sign in");

        var table = await client.GetFromJsonAsync<TranslationTableResponse>("/api/translations/en");

        table.ShouldNotBeNull();
        table.Tokens.ShouldContain(t => t.Text == "Sign in" && t.Translation == null);
    }

    [Fact]
    public async Task GetTranslations_UnknownLanguage_Returns404()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/translations/es");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReportUnknownTranslations_NewText_CreatesTokenAndBackfillsEnabledLanguages()
    {
        var adminClient = await _fixture.CreateAuthenticatedClientAsync();
        await CreateLanguageAsync(adminClient, "es");
        var client = _fixture.CreateClient();

        await ReportAsync(client, "Cancel");

        var stored = await _fixture.QueryDbAsync(db =>
            db.Set<TranslationToken>().AsNoTracking().SingleAsync(t => t.Text == "Cancel"));
        stored.ReportCount.ShouldBe(1);

        var translation = await _fixture.QueryDbAsync(db =>
            db.Set<Translation>().AsNoTracking().SingleAsync(t => t.TokenId == stored.Id && t.LanguageCode == "es"));
        translation.TranslatedText.ShouldBeNull();
    }

    [Fact]
    public async Task ReportUnknownTranslations_KnownText_BumpsCountWithoutDuplicating()
    {
        var client = _fixture.CreateClient();
        await ReportAsync(client, "Save");
        await ReportAsync(client, "Save");
        await ReportAsync(client, "Save");

        var tokens = await _fixture.QueryDbAsync(db =>
            db.Set<TranslationToken>().AsNoTracking().Where(t => t.Text == "Save").ToListAsync());

        tokens.Count.ShouldBe(1);
        tokens[0].ReportCount.ShouldBe(3);
    }

    [Fact]
    public async Task ReportUnknownTranslations_EmptyTexts_Returns400()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/translations/report", new { Texts = Array.Empty<string>() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrEnableLanguage_WithoutToken_Returns401()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/languages",
            new { Code = "es", EnglishName = "Spanish", NativeName = "Español", IsEnabled = true });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateOrEnableLanguage_RejectsEnglish()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/languages",
            new { Code = "en", EnglishName = "English", NativeName = "English", IsEnabled = true });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrEnableLanguage_NewlyEnabled_BackfillsExistingTokens()
    {
        var anonClient = _fixture.CreateClient();
        await ReportAsync(anonClient, "Welcome");
        var adminClient = await _fixture.CreateAuthenticatedClientAsync();

        await CreateLanguageAsync(adminClient, "es");

        var token = await _fixture.QueryDbAsync(db =>
            db.Set<TranslationToken>().AsNoTracking().SingleAsync(t => t.Text == "Welcome"));
        var backfilled = await _fixture.QueryDbAsync(db =>
            db.Set<Translation>().AsNoTracking().SingleOrDefaultAsync(t => t.TokenId == token.Id && t.LanguageCode == "es"));
        backfilled.ShouldNotBeNull();
        backfilled!.TranslatedText.ShouldBeNull();
    }

    [Fact]
    public async Task SetTranslation_ValidPair_UpdatesTextAndIsVisibleOnFetch()
    {
        var adminClient = await _fixture.CreateAuthenticatedClientAsync();
        await CreateLanguageAsync(adminClient, "es");
        var anonClient = _fixture.CreateClient();
        await ReportAsync(anonClient, "Cancel");
        var tokenId = await _fixture.QueryDbAsync(db =>
            db.Set<TranslationToken>().AsNoTracking().Where(t => t.Text == "Cancel").Select(t => t.Id).SingleAsync());

        var response = await adminClient.PatchAsJsonAsync($"/api/translations/es/{tokenId}",
            new { TranslatedText = "Cancelar" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var table = await anonClient.GetFromJsonAsync<TranslationTableResponse>("/api/translations/es");
        table!.Tokens.ShouldContain(t => t.Text == "Cancel" && t.Translation == "Cancelar");
    }

    [Fact]
    public async Task SetTranslation_UnknownTokenId_Returns404()
    {
        var adminClient = await _fixture.CreateAuthenticatedClientAsync();
        await CreateLanguageAsync(adminClient, "es");

        var response = await adminClient.PatchAsJsonAsync("/api/translations/es/999999",
            new { TranslatedText = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTranslations_SinceFilter_OnlyReturnsChangedEntries()
    {
        var adminClient = await _fixture.CreateAuthenticatedClientAsync();
        await CreateLanguageAsync(adminClient, "es");
        var anonClient = _fixture.CreateClient();
        await ReportAsync(anonClient, "Old string");
        // AsOf goes over the wire as whole Unix seconds (DateTime2UnixSerializer floors
        // the fractional part), so the reconstructed `since` can land up to ~1s *before*
        // the real server moment it represents. Wait past that before capturing AsOf, or
        // "Old string"'s write could still land after the floored `since` and reappear.
        await Task.Delay(1100);

        var asOfUnixSeconds = (await anonClient.GetFromJsonAsync<TranslationTableResponse>("/api/translations/es"))!.AsOf;
        var since = DateTimeOffset.FromUnixTimeSeconds(asOfUnixSeconds).UtcDateTime;

        await ReportAsync(anonClient, "New string");
        var newTokenId = await _fixture.QueryDbAsync(db =>
            db.Set<TranslationToken>().AsNoTracking().Where(t => t.Text == "New string").Select(t => t.Id).SingleAsync());
        await adminClient.PatchAsJsonAsync($"/api/translations/es/{newTokenId}", new { TranslatedText = "Nueva cadena" });

        var delta = await anonClient.GetFromJsonAsync<TranslationTableResponse>($"/api/translations/es?since={since:O}");

        delta!.Tokens.ShouldContain(t => t.Text == "New string" && t.Translation == "Nueva cadena");
        delta.Tokens.ShouldNotContain(t => t.Text == "Old string");
    }

    [Fact]
    public async Task GetLanguages_WorksWithoutAuthAndOnlyReturnsEnabled()
    {
        var adminClient = await _fixture.CreateAuthenticatedClientAsync();
        await CreateLanguageAsync(adminClient, "es");
        await CreateLanguageAsync(adminClient, "fr", isEnabled: false);
        var client = _fixture.CreateClient();

        var languages = (await client.GetFromJsonAsync<List<LanguageResponse>>("/api/languages"))!;

        languages.ShouldContain(l => l.Code == "es");
        languages.ShouldNotContain(l => l.Code == "fr");
    }

    [Fact]
    public async Task GetAllLanguages_WithoutToken_Returns401()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/languages/all");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllLanguages_IncludesDisabled()
    {
        var adminClient = await _fixture.CreateAuthenticatedClientAsync();
        await CreateLanguageAsync(adminClient, "es");
        await CreateLanguageAsync(adminClient, "fr", isEnabled: false);

        var languages = (await adminClient.GetFromJsonAsync<List<LanguageResponse>>("/api/languages/all"))!;

        languages.ShouldContain(l => l.Code == "es" && l.IsEnabled);
        languages.ShouldContain(l => l.Code == "fr" && !l.IsEnabled);
    }

    [Fact]
    public async Task GetTranslationTokens_WithoutToken_Returns401()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/translations/es/tokens");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTranslationTokens_UnknownLanguage_Returns404()
    {
        var adminClient = await _fixture.CreateAuthenticatedClientAsync();

        var response = await adminClient.GetAsync("/api/translations/es/tokens");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTranslationTokens_ReturnsTokenIdsAndWorksForADisabledLanguage()
    {
        var adminClient = await _fixture.CreateAuthenticatedClientAsync();
        // Enabled while the token is reported (so it gets backfilled a row), then
        // disabled again — existing rows survive a disable, only new tokens stop
        // getting backfilled for it going forward.
        await CreateLanguageAsync(adminClient, "es");
        var anonClient = _fixture.CreateClient();
        await ReportAsync(anonClient, "Cancel");
        var expectedTokenId = await _fixture.QueryDbAsync(db =>
            db.Set<TranslationToken>().AsNoTracking().Where(t => t.Text == "Cancel").Select(t => t.Id).SingleAsync());
        await CreateLanguageAsync(adminClient, "es", isEnabled: false);

        var tokens = (await adminClient.GetFromJsonAsync<List<TranslationTokenAdminResponse>>("/api/translations/es/tokens"))!;

        var cancelToken = tokens.ShouldHaveSingleItem();
        cancelToken.TokenId.ShouldBe(expectedTokenId);
        cancelToken.Text.ShouldBe("Cancel");
        cancelToken.Translation.ShouldBeNull();
        cancelToken.ReportCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetTranslationTokens_TranslatedEntryIncludesTheTranslation()
    {
        var adminClient = await _fixture.CreateAuthenticatedClientAsync();
        await CreateLanguageAsync(adminClient, "es");
        var anonClient = _fixture.CreateClient();
        await ReportAsync(anonClient, "Cancel");
        var tokenId = await _fixture.QueryDbAsync(db =>
            db.Set<TranslationToken>().AsNoTracking().Where(t => t.Text == "Cancel").Select(t => t.Id).SingleAsync());
        await adminClient.PatchAsJsonAsync($"/api/translations/es/{tokenId}", new { TranslatedText = "Cancelar" });

        var tokens = (await adminClient.GetFromJsonAsync<List<TranslationTokenAdminResponse>>("/api/translations/es/tokens"))!;

        tokens.ShouldContain(t => t.TokenId == tokenId && t.Translation == "Cancelar");
    }

    private static async Task ReportAsync(HttpClient client, string text)
    {
        var response = await client.PostAsJsonAsync("/api/translations/report", new { Texts = new[] { text } });
        response.EnsureSuccessStatusCode();
    }

    private static async Task CreateLanguageAsync(HttpClient adminClient, string code, bool isEnabled = true)
    {
        var response = await adminClient.PostAsJsonAsync("/api/languages",
            new { Code = code, EnglishName = code, NativeName = code, IsEnabled = isEnabled });
        response.EnsureSuccessStatusCode();
    }

    private sealed record TranslationEntryResponse(string Text, string? Translation);
    // AsOf comes over the wire as Unix seconds (DateTime2UnixSerializer, registered
    // globally) — GetFromJsonAsync here uses plain default options, not the app's, so
    // read it as the raw long rather than DateTime.
    private sealed record TranslationTableResponse(long AsOf, List<TranslationEntryResponse> Tokens);
    private sealed record LanguageResponse(string Code, string EnglishName, string NativeName, bool IsEnabled);

    private sealed record TranslationTokenAdminResponse(
        int TokenId,
        string Text,
        string? Context,
        string? Translation,
        int ReportCount,
        long LastSeenAt);
}
