using System.ComponentModel.DataAnnotations;

namespace api_server.Companies
{
	/// <summary>
	/// Body for POST /api/company-admin/user-groups: a new group of the caller's current company and, optionally,
	/// the members to start it with (<see cref="AddUserIds"/> must be members of the company).
	/// </summary>
	public class UserGroupCreateDto : IValidatableObject
	{
		[StringLength(UserGroupLimits.NameMaxLength)]
		public string Name { get; set; } = default!;

		[StringLength(UserGroupLimits.DescriptionMaxLength)]
		public string? Description { get; set; }

		public short GroupColor { get; set; }

		public List<int>? AddUserIds { get; set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (string.IsNullOrWhiteSpace(Name))
			{
				yield return new ValidationResult("Name cannot be empty.", [nameof(Name)]);
			}
			if (GroupColor < 0)
			{
				yield return new ValidationResult("GroupColor cannot be negative.", [nameof(GroupColor)]);
			}
		}
	}

	public static class UserGroupLimits
	{
		public const int NameMaxLength = 100;
		public const int DescriptionMaxLength = 500;
	}
}
