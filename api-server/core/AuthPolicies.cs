namespace api_server.core
{
	/// <summary>
	/// Named authorization policies, registered in Program.cs. A role policy is decided from the database on every call
	/// (see RoleAuthorizationHandler): the caller's role in the company they are working in, not a claim of the token.
	///
	/// Usage on an endpoint:
	///   [Authorize(Policy = AuthPolicies.PortalAdmin)]
	///   public IActionResult DevelopersOnlyAction() ...
	///
	/// A signed-in caller without the role gets 403; no login gets 401.
	/// </summary>
	public static class AuthPolicies
	{
		/// <summary>The developers' screens (Portal Admin menu): members of the system-wide role <see cref="PortalAdminRole"/>.</summary>
		public const string PortalAdmin = "PortalAdmin";

		/// <summary>permission_role.Name of the role (CompanyId null), created by main-database/custom-sql/insert-portal-admin-role.sql.</summary>
		public const string PortalAdminRole = "Portal Admin";
	}
}
