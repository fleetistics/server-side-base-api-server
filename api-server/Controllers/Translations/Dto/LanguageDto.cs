using db_model.Translations;

namespace api_server.Controllers.Translations.Dto
{
	public class LanguageDto
	{
		public LanguageDto() { }

		public LanguageDto(Language language)
		{
			Code = language.Code;
			EnglishName = language.EnglishName;
			NativeName = language.NativeName;
			IsEnabled = language.IsEnabled;
		}

		public string Code { get; set; } = default!;
		public string EnglishName { get; set; } = default!;
		public string NativeName { get; set; } = default!;
		public bool IsEnabled { get; set; }
	}
}
