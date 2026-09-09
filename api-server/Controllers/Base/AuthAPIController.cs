using api_server.Auth;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;

namespace api_server.Controllers.Base
{
	// Every endpoint that identifies the caller via UserId/SessionId claims requires a
	// valid access token; there is no global fallback policy, so it is enforced here.
	[Authorize]
	public class AuthAPIController: BaseAPIController
	{
		protected AuthAPIController(): base() { }
		protected int UserId => int.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? throw new Exception("UserId claim not found"));
		protected int SessionId => int.Parse(User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value ?? throw new Exception("SessionId claim not found"));

		// Not every session is tied to a physical device (e.g. web sessions), so
		// unlike UserId/SessionId this is nullable instead of throwing when absent.
		protected int? MobileGpsDeviceId
		{
			get
			{
				var value = User.FindFirst(TokenService.MobileGpsDeviceIdClaim)?.Value;
				return value is null ? null : int.Parse(value);
			}
		}
	}
}
