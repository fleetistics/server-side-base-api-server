using System.ComponentModel.DataAnnotations;

namespace api_server.Controllers.Translations.Dto
{
	/// <summary>Batch of English strings the client's t() couldn't find in its loaded table.</summary>
	public class ReportUnknownTranslationsDto : IValidatableObject
	{
		public IReadOnlyList<string> Texts { get; set; } = [];

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (Texts is not { Count: > 0 } || Texts.All(string.IsNullOrWhiteSpace))
			{
				yield return new ValidationResult("Texts must contain at least one non-empty string.", [nameof(Texts)]);
			}
		}
	}
}
