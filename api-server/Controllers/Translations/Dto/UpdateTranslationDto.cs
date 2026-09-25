using api_server.core;

namespace api_server.Controllers.Translations.Dto
{
	/// <summary>Translator sets (or clears, via null) one token's text in one language.</summary>
	public class UpdateTranslationDto
	{
		[MeaningfulNull]
		public string? TranslatedText { get; set; }
	}
}
