namespace api_server.Controllers.Translations.Dto
{
	public class TranslationEntryDto
	{
		public TranslationEntryDto() { }

		public TranslationEntryDto(string text, string? translation)
		{
			Text = text;
			Translation = translation;
		}

		public string Text { get; set; } = default!;

		// Null means "no translation yet" — this covers both an untranslated token in a
		// real target language and, for lang=en, every entry (English has no Translation
		// row at all). Callers already have to handle the null-fallback-to-English case,
		// so treating "en" as producing all-null entries needs no special client logic.
		public string? Translation { get; set; }
	}
}
