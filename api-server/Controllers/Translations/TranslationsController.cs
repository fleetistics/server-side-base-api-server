using api_server.Controllers.Base;
using api_server.Controllers.Translations.Dto;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Translations
{
	// Anonymous on purpose: the login page itself needs translated labels before any
	// token exists, so the translation table and the language list must be reachable
	// pre-auth. Contrast with TranslationsAdminController, which requires a session.
	public class TranslationsController : BaseAPIController
	{
		public TranslationsController(ITranslationService translations)
		{
			mTranslations = translations;
		}

		[HttpGet("/api/translations/{lang}")]
		[ProducesResponseType(typeof(TranslationTableDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetTranslations([FromRoute] string lang, [FromQuery] DateTime? since, CancellationToken cancellationToken)
		{
			var table = await mTranslations.GetTranslationTableAsync(lang, since, cancellationToken);
			if (table == null)
			{
				return NotFound(new { message = "Unknown or disabled language." });
			}
			return Ok(table);
		}

		[HttpGet("/api/languages")]
		[ProducesResponseType(typeof(IEnumerable<LanguageDto>), StatusCodes.Status200OK)]
		public async Task<IActionResult> GetLanguages(CancellationToken cancellationToken)
		{
			return Ok(await mTranslations.GetEnabledLanguagesAsync(cancellationToken));
		}

		[HttpPost("/api/translations/report")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		public async Task<IActionResult> ReportUnknownTranslations([FromBody] ReportUnknownTranslationsDto data, CancellationToken cancellationToken)
		{
			await mTranslations.ReportUnknownTranslationsAsync(data.Texts, cancellationToken);
			return NoContent();
		}

		private readonly ITranslationService mTranslations;
	}
}
