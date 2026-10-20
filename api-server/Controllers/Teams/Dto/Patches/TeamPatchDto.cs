using exs.commons.Json;
using System.ComponentModel.DataAnnotations;

namespace api_server.Teams
{
	public class TeamPatchDto : IValidatableObject
	{
		public Optional<string> TeamName { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (TeamName is { IsSet: true, Value: var value } && string.IsNullOrWhiteSpace(value))
			{
				yield return new ValidationResult("TeamName cannot be empty.", [nameof(TeamName)]);
			}
		}
	}
}
