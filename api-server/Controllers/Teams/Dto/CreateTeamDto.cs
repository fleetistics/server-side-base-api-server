using System.ComponentModel.DataAnnotations;

namespace api_server.Controllers.Teams.Dto
{
	/// <summary>
	/// Body for POST /api/team. CreatorUserId and CreatedDate are derived server-side (from the
	/// caller and from now, respectively), Id is DB-assigned, and JoinQrCode is server-generated,
	/// so none of those are part of the client-supplied body. StatusId defaults to
	/// TeamStatus.Creating when omitted - a new team has no meaningful ClosedDate yet.
	/// </summary>
	public class CreateTeamDto : IValidatableObject
	{
		public string TeamName { get; set; } = default!;
		public short? StatusId { get; set; }
		public string? JoinKey { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (string.IsNullOrWhiteSpace(TeamName))
			{
				yield return new ValidationResult("TeamName is required.", [nameof(TeamName)]);
			}
		}
	}
}
