namespace api_server.Controllers.Translations.Dto
{
	public class TranslationTableDto
	{
		public TranslationTableDto() { }

		public TranslationTableDto(DateTime asOf, IReadOnlyList<TranslationEntryDto> tokens)
		{
			AsOf = asOf;
			Tokens = tokens;
		}

		// Server clock at query time — the client's background updater passes this back
		// as `since` on the next poll, so a slow client/network gap between AsOf and the
		// response reaching the browser never causes a missed update.
		public DateTime AsOf { get; set; }

		public IReadOnlyList<TranslationEntryDto> Tokens { get; set; } = [];
	}
}
