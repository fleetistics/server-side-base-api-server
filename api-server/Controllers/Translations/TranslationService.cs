using api_server.Controllers.Translations.Dto;
using db_model.Translations;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Translations
{
	public interface ITranslationService
	{
		/// <summary>
		/// Null when <paramref name="lang"/> is neither "en" nor a known enabled language.
		/// Pass <paramref name="since"/> to get only entries changed after that point.
		/// </summary>
		Task<TranslationTableDto?> GetTranslationTableAsync(string lang, DateTime? since, CancellationToken cancellationToken);

		Task<IReadOnlyList<LanguageDto>> GetEnabledLanguagesAsync(CancellationToken cancellationToken);

		/// <summary>Admin view: every language, enabled or not.</summary>
		Task<IReadOnlyList<LanguageDto>> GetAllLanguagesAsync(CancellationToken cancellationToken);

		/// <summary>
		/// Admin view of every token's translation into <paramref name="lang"/>, including
		/// TokenId. Unlike GetTranslationTableAsync, works for a disabled language too (an
		/// admin needs to see/edit those, not just enabled ones) — null only when the
		/// language code has never been created at all.
		/// </summary>
		Task<IReadOnlyList<TranslationTokenAdminDto>?> GetTranslationTokensAsync(string lang, CancellationToken cancellationToken);

		/// <summary>
		/// For each text: bumps ReportCount/LastSeenAt if the token is already known, or
		/// creates it and eagerly backfills a NULL Translation row for every enabled
		/// language. Blank/duplicate entries are ignored rather than rejected.
		/// </summary>
		Task ReportUnknownTranslationsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);

		/// <summary>Creates a language, or updates+re-enables one that exists. Newly enabling one backfills every existing token.</summary>
		Task<LanguageDto> CreateOrEnableLanguageAsync(CreateLanguageDto data, CancellationToken cancellationToken);

		/// <summary>Null when no Translation row exists for this (tokenId, lang) pair.</summary>
		Task<TranslationEntryDto?> SetTranslationAsync(string lang, int tokenId, string? translatedText, int updatedByUserId, CancellationToken cancellationToken);
	}

	public sealed class TranslationService : ITranslationService
	{
		private readonly IRepository mRepository;

		public TranslationService(IRepository repository)
		{
			mRepository = repository;
		}

		public async Task<TranslationTableDto?> GetTranslationTableAsync(string lang, DateTime? since, CancellationToken cancellationToken)
		{
			var asOf = DateTime.UtcNow;

			if (string.Equals(lang, Language.English, StringComparison.OrdinalIgnoreCase))
			{
				var tokenQuery = mRepository.GetQueryable<TranslationToken>();
				if (since.HasValue)
				{
					tokenQuery = tokenQuery.Where(t => t.FirstSeenAt > since.Value);
				}
				var knownTexts = await tokenQuery
					.AsNoTracking()
					.Select(t => new TranslationEntryDto(t.Text, null))
					.ToListAsync(cancellationToken);
				return new TranslationTableDto(asOf, knownTexts);
			}

			var language = await mRepository.GetQueryable<Language>(l => l.Code == lang && l.IsEnabled)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			if (language == null)
			{
				return null;
			}

			var translationQuery = mRepository.GetQueryable<Translation>(t => t.LanguageCode == lang);
			if (since.HasValue)
			{
				translationQuery = translationQuery.Where(t => t.UpdatedAt > since.Value);
			}
			var entries = await translationQuery
				.Include(t => t.Token)
				.AsNoTracking()
				.Select(t => new TranslationEntryDto(t.Token!.Text, t.TranslatedText))
				.ToListAsync(cancellationToken);
			return new TranslationTableDto(asOf, entries);
		}

		public async Task<IReadOnlyList<LanguageDto>> GetEnabledLanguagesAsync(CancellationToken cancellationToken)
		{
			var languages = await mRepository.GetQueryable<Language>(l => l.IsEnabled)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return languages.Select(l => new LanguageDto(l)).ToList();
		}

		public async Task<IReadOnlyList<LanguageDto>> GetAllLanguagesAsync(CancellationToken cancellationToken)
		{
			var languages = await mRepository.GetQueryable<Language>()
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return languages.Select(l => new LanguageDto(l)).ToList();
		}

		public async Task<IReadOnlyList<TranslationTokenAdminDto>?> GetTranslationTokensAsync(string lang, CancellationToken cancellationToken)
		{
			var languageExists = await mRepository.GetQueryable<Language>(l => l.Code == lang)
				.AnyAsync(cancellationToken);
			if (!languageExists)
			{
				return null;
			}

			return await mRepository.GetQueryable<Translation>(t => t.LanguageCode == lang)
				.Include(t => t.Token)
				.AsNoTracking()
				.Select(t => new TranslationTokenAdminDto(
					t.TokenId,
					t.Token!.Text,
					t.Token.Context,
					t.TranslatedText,
					t.Token.ReportCount,
					t.Token.LastSeenAt))
				.ToListAsync(cancellationToken);
		}

		public async Task ReportUnknownTranslationsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
		{
			var distinctTexts = texts
				.Where(t => !string.IsNullOrWhiteSpace(t))
				.Select(t => t.Trim())
				.Distinct();

			foreach (var text in distinctTexts)
			{
				await ReportOneAsync(text, cancellationToken);
			}
		}

		private async Task ReportOneAsync(string text, CancellationToken cancellationToken)
		{
			var existing = await mRepository.GetQueryable<TranslationToken>(t => t.Text == text)
				.FirstOrDefaultAsync(cancellationToken);
			if (existing != null)
			{
				existing.LastSeenAt = DateTime.UtcNow;
				existing.ReportCount += 1;
				await mRepository.SaveAsync(cancellationToken);
				return;
			}

			var token = new TranslationToken
			{
				Text = text,
				FirstSeenAt = DateTime.UtcNow,
				LastSeenAt = DateTime.UtcNow,
				ReportCount = 1,
			};
			mRepository.Create(token);
			try
			{
				await mRepository.SaveAsync(cancellationToken);
			}
			catch (DbUpdateException)
			{
				// Another request reported the same brand-new string first (race on the
				// unique Text index) — fall back to bumping the winner's counters instead,
				// matching the "ON CONFLICT DO NOTHING" semantics this was designed around.
				mRepository.Detach(token);
				await ReportOneAsync(text, cancellationToken);
				return;
			}

			await BackfillTranslationRowsForNewTokenAsync(token.Id, cancellationToken);
		}

		private async Task BackfillTranslationRowsForNewTokenAsync(int tokenId, CancellationToken cancellationToken)
		{
			var enabledLanguageCodes = await mRepository.GetQueryable<Language>(l => l.IsEnabled)
				.Select(l => l.Code)
				.ToListAsync(cancellationToken);
			if (enabledLanguageCodes.Count == 0)
			{
				return;
			}

			foreach (var code in enabledLanguageCodes)
			{
				mRepository.Create(new Translation
				{
					TokenId = tokenId,
					LanguageCode = code,
					TranslatedText = null,
					UpdatedAt = DateTime.UtcNow,
				});
			}
			await mRepository.SaveAsync(cancellationToken);
		}

		private async Task BackfillTranslationRowsForNewLanguageAsync(string languageCode, CancellationToken cancellationToken)
		{
			var tokenIdsWithARow = await mRepository.GetQueryable<Translation>(t => t.LanguageCode == languageCode)
				.Select(t => t.TokenId)
				.ToListAsync(cancellationToken);
			var covered = tokenIdsWithARow.ToHashSet();

			var missingTokenIds = await mRepository.GetQueryable<TranslationToken>()
				.Select(t => t.Id)
				.Where(id => !covered.Contains(id))
				.ToListAsync(cancellationToken);
			if (missingTokenIds.Count == 0)
			{
				return;
			}

			foreach (var tokenId in missingTokenIds)
			{
				mRepository.Create(new Translation
				{
					TokenId = tokenId,
					LanguageCode = languageCode,
					TranslatedText = null,
					UpdatedAt = DateTime.UtcNow,
				});
			}
			await mRepository.SaveAsync(cancellationToken);
		}

		public async Task<LanguageDto> CreateOrEnableLanguageAsync(CreateLanguageDto data, CancellationToken cancellationToken)
		{
			var language = await mRepository.GetQueryable<Language>(l => l.Code == data.Code)
				.FirstOrDefaultAsync(cancellationToken);
			var wasEnabled = language?.IsEnabled ?? false;

			if (language == null)
			{
				language = new Language
				{
					Code = data.Code,
					EnglishName = data.EnglishName,
					NativeName = data.NativeName,
					IsEnabled = data.IsEnabled,
				};
				mRepository.Create(language);
			}
			else
			{
				language.EnglishName = data.EnglishName;
				language.NativeName = data.NativeName;
				language.IsEnabled = data.IsEnabled;
			}
			await mRepository.SaveAsync(cancellationToken);

			if (!wasEnabled && language.IsEnabled)
			{
				await BackfillTranslationRowsForNewLanguageAsync(language.Code, cancellationToken);
			}

			return new LanguageDto(language);
		}

		public async Task<TranslationEntryDto?> SetTranslationAsync(string lang, int tokenId, string? translatedText, int updatedByUserId, CancellationToken cancellationToken)
		{
			var translation = await mRepository.GetQueryable<Translation>(t => t.TokenId == tokenId && t.LanguageCode == lang)
				.Include(t => t.Token)
				.FirstOrDefaultAsync(cancellationToken);
			if (translation == null)
			{
				return null;
			}

			translation.TranslatedText = translatedText;
			translation.UpdatedAt = DateTime.UtcNow;
			translation.UpdatedByUserId = updatedByUserId;
			await mRepository.SaveAsync(cancellationToken);

			return new TranslationEntryDto(translation.Token!.Text, translation.TranslatedText);
		}
	}
}
