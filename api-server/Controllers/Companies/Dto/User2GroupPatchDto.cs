using System.ComponentModel.DataAnnotations;

namespace api_server.Companies
{
	/// <summary>
	/// Partial-update body for PATCH /api/company-admin/user2group/{userId}: the groups to add the user to and
	/// the groups to take the user out of. Sending changes rather than the full list means two admins editing
	/// the same user don't overwrite each other's memberships. Groups the user is already in (Add) or not in
	/// (Remove) are skipped. Omitted lists change nothing.
	/// </summary>
	public class User2GroupPatchDto : IValidatableObject
	{
		public List<int>? AddGroupIds { get; set; }
		public List<int>? RemoveGroupIds { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (AddGroupIds is not { Count: > 0 } && RemoveGroupIds is not { Count: > 0 })
			{
				yield return new ValidationResult("Nothing to change: set AddGroupIds and/or RemoveGroupIds.", [nameof(AddGroupIds), nameof(RemoveGroupIds)]);
			}
			else if (AddGroupIds != null && RemoveGroupIds != null && AddGroupIds.Intersect(RemoveGroupIds).Any())
			{
				yield return new ValidationResult("A group cannot be both added and removed.", [nameof(AddGroupIds), nameof(RemoveGroupIds)]);
			}
		}
	}
}
