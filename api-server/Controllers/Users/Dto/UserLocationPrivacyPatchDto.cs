using exs.commons.Json;

namespace api_server.Controllers.Users.Dto
{
	/// <summary>Partial-update body for PATCH /api/users/me/location-privacy.</summary>
	public class UserLocationPrivacyPatchDto
	{
		public Optional<short> PrivacyMode { get; set; }
	}
}
