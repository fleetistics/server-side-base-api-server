using exs.commons.Json;
using System.ComponentModel.DataAnnotations;

namespace api_server.Controllers.Teams.Dto
{
	/// <summary>
	/// Partial-update body for PATCH /api/team/{teamId} (and .../details): only fields the
	/// client actually changed are present. 404 if the team doesn't exist - creation only
	/// happens via POST /api/team.
	/// </summary>
	public class TeamPatchDto : IValidatableObject
	{
		public Optional<string> TeamName { get; set; }
		public Optional<short> StatusId { get; set; }
		public Optional<DateTime?> ClosedDate { get; set; }
		public Optional<string?> JoinKey { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (TeamName is { IsSet: true, Value: var value } && string.IsNullOrWhiteSpace(value))
			{
				yield return new ValidationResult("TeamName cannot be empty.", [nameof(TeamName)]);
			}
		}
	}
}
