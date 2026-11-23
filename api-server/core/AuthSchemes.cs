using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace api_server.core
{
	/// <summary>
	/// The JWT bearer schemes registered in Program.cs. They differ only in the audience they accept, which is
	/// what keeps the two kinds of access token (TokenService.AccessTokenKind) apart:
	///   Api            - the default scheme; AuthAPIController's [Authorize] uses it.
	///   LocationReport - only LocationAuthAPIController names it.
	/// </summary>
	public static class AuthSchemes
	{
		public const string Api = JwtBearerDefaults.AuthenticationScheme;
		public const string LocationReport = "LocationReportBearer";
	}
}
