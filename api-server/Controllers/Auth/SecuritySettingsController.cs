using api_server.Auth;
using api_server.Auth.Dto;
using api_server.Controllers.Base;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Auth
{
	public class SecuritySettingsController : AuthAPIController
	{
		public SecuritySettingsController(ISecuritySettingsService securitySettings)
		{
			mSecuritySettings = securitySettings;
		}

		/// <summary>Security settings for the caller's current company, falling back to the system-wide ones.</summary>
		[HttpGet("/api/auth/security-settings")]
		[ProducesResponseType(typeof(SecuritySettingsDto), StatusCodes.Status200OK)]
		public async Task<IActionResult> GetSecuritySettings(CancellationToken cancellationToken)
		{
			var result = await mSecuritySettings.GetSecuritySettingsAsync(CompanyId, cancellationToken);
			return Ok(result);
		}

		private readonly ISecuritySettingsService mSecuritySettings;
	}
}
