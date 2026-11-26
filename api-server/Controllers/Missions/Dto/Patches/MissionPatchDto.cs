using exs.commons.Json;
using System.ComponentModel.DataAnnotations;

namespace api_server.Missions
{
	public class MissionPatchDto : IValidatableObject
	{
		// Named after the Mission entity column it maps onto (see PatchExtensions.ApplyPatch).
		public Optional<string> Name { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (Name is { IsSet: true, Value: var value } && string.IsNullOrWhiteSpace(value))
			{
				yield return new ValidationResult("Name cannot be empty.", new[] { nameof(Name) });
			}
		}
	}
}
