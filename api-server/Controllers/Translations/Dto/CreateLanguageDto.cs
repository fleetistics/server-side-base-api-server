using System.ComponentModel.DataAnnotations;
using db_model.Translations;

namespace api_server.Controllers.Translations.Dto
{
	/// <summary>
	/// Creates a language, or updates+re-enables one that already exists. Enabling a
	/// language (whether new or previously disabled) triggers the eager backfill of
	/// NULL Translation rows for every existing token.
	/// </summary>
	public class CreateLanguageDto : IValidatableObject
	{
		public string Code { get; set; } = default!;
		public string EnglishName { get; set; } = default!;
		public string NativeName { get; set; } = default!;
		public bool IsEnabled { get; set; } = true;

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (string.IsNullOrWhiteSpace(Code))
			{
				yield return new ValidationResult("Code is required.", [nameof(Code)]);
			}
			else if (string.Equals(Code, Language.English, StringComparison.OrdinalIgnoreCase))
			{
				yield return new ValidationResult(
					"English is the source language and is never a row in the language table.",
					[nameof(Code)]);
			}
		}
	}
}
