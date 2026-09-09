using api_server.Controllers.Media.Dto;
using exs.commons.Json;
using System.ComponentModel.DataAnnotations;

namespace api_server.Controllers.Users.Dto
{
	/// <summary>
	/// Partial-update body for PATCH /api/users/{userId}: only fields the client
	/// actually changed are present. UserName/StatusId are excluded — UpdateUserAsync
	/// (the full-PUT path) never touches them either.
	/// </summary>
	public class UserPatchDto : InboundMediaAttachableDto, IValidatableObject
    {
		public Optional<string> DisplayName { get; set; }
		public Optional<string?> FullName { get; set; }
		public Optional<string?> Phone { get; set; }
		public Optional<string?> Email { get; set; }

		// Optional<T> bypasses the implicit NRT-required model validation UserDto gets
		// for free (the wrapper is always structurally "present"), so the one rule that
		// still needs enforcing — DisplayName can never be cleared to empty — is opted
		// back in here. [ApiController]'s automatic model validation runs this the same
		// way it already runs for UserDto, producing the same 400 ValidationProblemDetails
		// shape without any special-casing in the controller or service.
		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (DisplayName is { IsSet: true, Value: var value } && string.IsNullOrWhiteSpace(value))
			{
				yield return new ValidationResult("DisplayName cannot be empty.", [nameof(DisplayName)]);
			}
		}
	}
}
