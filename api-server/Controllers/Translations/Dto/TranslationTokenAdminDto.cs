namespace api_server.Controllers.Translations.Dto
{
	/// <summary>
	/// The admin/translator view of one token in one language — unlike TranslationEntryDto,
	/// this exposes TokenId (needed to build the PATCH URL) and the discovery metadata that
	/// helps a translator prioritize what's actually used.
	/// </summary>
	public class TranslationTokenAdminDto
	{
		public TranslationTokenAdminDto() { }

		public TranslationTokenAdminDto(
			int tokenId,
			string text,
			string? context,
			string? translation,
			int reportCount,
			DateTime lastSeenAt)
		{
			TokenId = tokenId;
			Text = text;
			Context = context;
			Translation = translation;
			ReportCount = reportCount;
			LastSeenAt = lastSeenAt;
		}

		public int TokenId { get; set; }
		public string Text { get; set; } = default!;
		public string? Context { get; set; }
		public string? Translation { get; set; }
		public int ReportCount { get; set; }
		public DateTime LastSeenAt { get; set; }
	}
}
