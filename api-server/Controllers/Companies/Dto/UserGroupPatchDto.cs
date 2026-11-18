using System.ComponentModel.DataAnnotations;
using exs.commons.Json;

namespace api_server.Companies
{
	/// <summary>
	/// Partial-update body for PATCH /api/company-admin/user-groups/{groupId}: only the fields present are applied,
	/// as with UserPatchDto. Members are sent as changes (<see cref="AddUserIds"/> / <see cref="RemoveUserIds"/>),
	/// not as the full list, so two admins editing the same group don't overwrite each other's memberships.
	/// Users already in the group (Add) or not in it (Remove) are skipped.
	/// </summary>
	public class UserGroupPatchDto : IValidatableObject
	{
		public Optional<string> Name { get; set; }
		public Optional<string?> Description { get; set; }
		public Optional<short> GroupColor { get; set; }
		public List<int>? AddUserIds { get; set; }
		public List<int>? RemoveUserIds { get; set; }

		// Optional<T> bypasses the implicit model validation, so the rules are opted back in here.
		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (!Name.IsSet && !Description.IsSet && !GroupColor.IsSet && AddUserIds is not { Count: > 0 } && RemoveUserIds is not { Count: > 0 })
			{
				yield return new ValidationResult("Nothing to change.", [nameof(Name), nameof(Description), nameof(GroupColor), nameof(AddUserIds), nameof(RemoveUserIds)]);
			}
			if (Name is { IsSet: true, Value: var name })
			{
				if (string.IsNullOrWhiteSpace(name)) yield return new ValidationResult("Name cannot be empty.", [nameof(Name)]);
				else if (name.Length > UserGroupLimits.NameMaxLength) yield return new ValidationResult($"Name cannot be longer than {UserGroupLimits.NameMaxLength} characters.", [nameof(Name)]);
			}
			if (Description is { IsSet: true, Value: { Length: > UserGroupLimits.DescriptionMaxLength } })
			{
				yield return new ValidationResult($"Description cannot be longer than {UserGroupLimits.DescriptionMaxLength} characters.", [nameof(Description)]);
			}
			if (GroupColor is { IsSet: true, Value: < 0 })
			{
				yield return new ValidationResult("GroupColor cannot be negative.", [nameof(GroupColor)]);
			}
			if (AddUserIds != null && RemoveUserIds != null && AddUserIds.Intersect(RemoveUserIds).Any())
			{
				yield return new ValidationResult("A user cannot be both added and removed.", [nameof(AddUserIds), nameof(RemoveUserIds)]);
			}
		}
	}
}
