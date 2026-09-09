using api_server.Controllers.Base;
using api_server.Controllers.Translations.Dto;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Translations
{
	// Authenticated: creating/enabling a language and writing translated text are
	// translator/admin actions, unlike the read/report endpoints on TranslationsController.
	public class TranslationsAdminController : AuthAPIController
	{
		public TranslationsAdminController(ITranslationService translations)
		{
			mTranslations = translations;
		}

		[HttpGet("/api/languages/all")]
		[ProducesResponseType(typeof(IEnumerable<LanguageDto>), StatusCodes.Status200OK)]
		public async Task<IActionResult> GetAllLanguages(CancellationToken cancellationToken)
		{
			return Ok(await mTranslations.GetAllLanguagesAsync(cancellationToken));
		}

		[HttpPost("/api/languages")]
		[ProducesResponseType(typeof(LanguageDto), StatusCodes.Status200OK)]
		public async Task<IActionResult> CreateOrEnableLanguage([FromBody] CreateLanguageDto data, CancellationToken cancellationToken)
		{
			return Ok(await mTranslations.CreateOrEnableLanguageAsync(data, cancellationToken));
		}

		[HttpGet("/api/translations/{lang}/tokens")]
		[ProducesResponseType(typeof(IEnumerable<TranslationTokenAdminDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetTranslationTokens([FromRoute] string lang, CancellationToken cancellationToken)
		{
			var tokens = await mTranslations.GetTranslationTokensAsync(lang, cancellationToken);
			if (tokens == null)
			{
				return NotFound(new { message = "Unknown language." });
			}
			return Ok(tokens);
		}

		[HttpPatch("/api/translations/{lang}/{tokenId}")]
		[ProducesResponseType(typeof(TranslationEntryDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> SetTranslation([FromRoute] string lang, [FromRoute] int tokenId, [FromBody] UpdateTranslationDto data, CancellationToken cancellationToken)
		{
			var updated = await mTranslations.SetTranslationAsync(lang, tokenId, data.TranslatedText, UserId, cancellationToken);
			if (updated == null)
			{
				return NotFound(new { message = "No translation row for this token/language pair." });
			}
			return Ok(updated);
		}

		private readonly ITranslationService mTranslations;
	}
}
