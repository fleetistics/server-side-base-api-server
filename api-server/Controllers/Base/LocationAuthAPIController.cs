using api_server.Auth;
using api_server.core;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;

namespace api_server.Controllers.Base
{
	// Base of the location-report endpoints: they accept only location-report access tokens
	// (TokenService.AccessTokenKind.LocationReport), never the API ones, and the API endpoints refuse these.
	//
	// It deliberately does NOT derive from AuthAPIController. That class's [Authorize] (default scheme) would be
	// inherited, and ASP.NET Core unions the schemes of all [Authorize] attributes on an action, so an API token
	// would be accepted here as well.
	[Authorize(AuthenticationSchemes = AuthSchemes.LocationReport)]
	public class LocationAuthAPIController: BaseAPIController
	{
		protected LocationAuthAPIController(): base() { }
		protected int UserId => int.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? throw new Exception("UserId claim not found"));
		protected int SessionId => int.Parse(User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value ?? throw new Exception("SessionId claim not found"));

		// Location-report tokens are only issued for sessions bound to a mobile GPS device, so the claim is required.
		protected int MobileGpsDeviceId => int.Parse(User.FindFirst(TokenService.MobileGpsDeviceIdClaim)?.Value ?? throw new Exception("MobileGpsDeviceId claim not found"));
        protected int? CompanyId
        {
            get
            {
                var value = User.FindFirst(TokenService.CompanyIdClaim)?.Value;
                return value is null ? null : int.Parse(value);
            }
        }
    }
}
