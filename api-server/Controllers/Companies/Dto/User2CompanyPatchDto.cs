using System.ComponentModel.DataAnnotations;
using exs.commons.Json;

namespace api_server.Companies
{
	/// <summary>
	/// Partial-update body for PATCH /api/company-admin/user2company/{userId}: only the fields present are
	/// applied, as with UserPatchDto. A company admin may change:
	/// <list type="bullet">
	/// <item><c>StatusId</c> - only to Active (reactivate), Suspended or Deleted; any other value is rejected by the service.</item>
	/// <item><c>PermissionRoleId</c> - to a system-wide role or one of the company's own.</item>
	/// </list>
	/// </summary>
	public class User2CompanyPatchDto : IValidatableObject
	{
		public Optional<short> StatusId { get; set; }
		public Optional<int> PermissionRoleId { get; set; }

		// Optional<T> bypasses the implicit "required" model validation, so the one rule that matters is opted
		// back in here: the body must change something. (A JSON null for either value-type field is already
		// rejected by the Optional converter, which can't read null as a short/int.)
		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (!StatusId.IsSet && !PermissionRoleId.IsSet)
			{
				yield return new ValidationResult("Nothing to change: set StatusId and/or PermissionRoleId.", [nameof(StatusId), nameof(PermissionRoleId)]);
			}
		}
	}
}
