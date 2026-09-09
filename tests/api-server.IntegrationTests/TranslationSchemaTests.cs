using db_model.Translations;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// No API/service layer exists for translations yet — these tests exercise the DbContext
/// directly to prove the schema itself (keys, unique constraints, FK relationships) holds
/// up against a real Postgres, ahead of any endpoint being built on top of it.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TranslationSchemaTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;

    public TranslationSchemaTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Language_CodeIsPrimaryKey_DuplicateInsertFails()
    {
        await _fixture.QueryDbAsync(async db =>
        {
            db.Set<Language>().Add(new Language { Code = "es", EnglishName = "Spanish", NativeName = "Español", IsEnabled = true });
            await db.SaveChangesAsync();
            return 0;
        });

        await Should.ThrowAsync<DbUpdateException>(() => _fixture.QueryDbAsync(async db =>
        {
            db.Set<Language>().Add(new Language { Code = "es", EnglishName = "Spanish (dup)", NativeName = "Español", IsEnabled = true });
            await db.SaveChangesAsync();
            return 0;
        }));
    }

    [Fact]
    public async Task TranslationToken_TextIsUnique_DuplicateInsertFails()
    {
        await _fixture.QueryDbAsync(async db =>
        {
            db.Set<TranslationToken>().Add(new TranslationToken
            {
                Text = "Sign in",
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                ReportCount = 1,
            });
            await db.SaveChangesAsync();
            return 0;
        });

        await Should.ThrowAsync<DbUpdateException>(() => _fixture.QueryDbAsync(async db =>
        {
            db.Set<TranslationToken>().Add(new TranslationToken
            {
                Text = "Sign in",
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                ReportCount = 1,
            });
            await db.SaveChangesAsync();
            return 0;
        }));
    }

    [Fact]
    public async Task Translation_TokenLanguagePairIsUnique_DuplicateInsertFails()
    {
        var (tokenId, _) = await SeedTokenAndLanguageAsync();

        await _fixture.QueryDbAsync(async db =>
        {
            db.Set<Translation>().Add(new Translation
            {
                TokenId = tokenId,
                LanguageCode = "es",
                TranslatedText = null,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return 0;
        });

        await Should.ThrowAsync<DbUpdateException>(() => _fixture.QueryDbAsync(async db =>
        {
            db.Set<Translation>().Add(new Translation
            {
                TokenId = tokenId,
                LanguageCode = "es",
                TranslatedText = "Iniciar sesión",
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return 0;
        }));
    }

    [Fact]
    public async Task Translation_ForeignKeysAreEnforced_UnknownTokenIdFails()
    {
        await Should.ThrowAsync<DbUpdateException>(() => _fixture.QueryDbAsync(async db =>
        {
            db.Set<Translation>().Add(new Translation
            {
                TokenId = -1,
                LanguageCode = "es",
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return 0;
        }));
    }

    [Fact]
    public async Task Translation_ForeignKeysAreEnforced_UnknownLanguageCodeFails()
    {
        var (tokenId, _) = await SeedTokenAndLanguageAsync(seedLanguage: false);

        await Should.ThrowAsync<DbUpdateException>(() => _fixture.QueryDbAsync(async db =>
        {
            db.Set<Translation>().Add(new Translation
            {
                TokenId = tokenId,
                LanguageCode = "xx",
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return 0;
        }));
    }

    [Fact]
    public async Task Translation_ValidRow_RoundTripsWithNullTranslatedText()
    {
        var (tokenId, _) = await SeedTokenAndLanguageAsync();

        await _fixture.QueryDbAsync(async db =>
        {
            db.Set<Translation>().Add(new Translation
            {
                TokenId = tokenId,
                LanguageCode = "es",
                TranslatedText = null,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return 0;
        });

        var stored = await _fixture.QueryDbAsync(db =>
            db.Set<Translation>().AsNoTracking().SingleAsync(t => t.TokenId == tokenId && t.LanguageCode == "es"));
        stored.TranslatedText.ShouldBeNull();
        stored.LatestUpdate.ShouldNotBe(default);
    }

    private async Task<(int tokenId, string languageCode)> SeedTokenAndLanguageAsync(bool seedLanguage = true)
    {
        var tokenId = await _fixture.QueryDbAsync(async db =>
        {
            var token = new TranslationToken
            {
                Text = "Sign in",
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                ReportCount = 1,
            };
            db.Set<TranslationToken>().Add(token);

            if (seedLanguage)
            {
                db.Set<Language>().Add(new Language { Code = "es", EnglishName = "Spanish", NativeName = "Español", IsEnabled = true });
            }

            await db.SaveChangesAsync();
            return token.Id;
        });

        return (tokenId, "es");
    }
}
