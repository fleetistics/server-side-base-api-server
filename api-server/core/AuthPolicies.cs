namespace api_server.core
{
	/// <summary>
	/// Named authorization policies, registered in Program.cs. Roles come from the
	/// access token (TokenService.CreateAccessToken adds them as role claims).
	///
	/// Usage on an endpoint:
	///   [Authorize(Policy = AuthPolicies.Admin)]
	///   public IActionResult AdminOnlyAction() ...
	///
	/// No endpoint uses Admin yet — it is the template's example of how role-based
	/// access is added without scattering magic strings.
	/// </summary>
	public static class AuthPolicies
	{
		public const string Admin = "Admin";
		public const string AdminRole = "admin";
	}
}
