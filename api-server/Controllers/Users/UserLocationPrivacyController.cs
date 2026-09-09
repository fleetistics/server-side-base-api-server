using api_server.Controllers.Base;
using api_server.Controllers.Users.Dto;
using api_server.Controllers.Users.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Users
{
	public class UserLocationPrivacyController : AuthAPIController
	{
		public UserLocationPrivacyController(IUserLocationPrivacyService userLocationPrivacy)
		{
			mUserLocationPrivacy = userLocationPrivacy;
		}

		[HttpGet("/api/users/me/location-privacy")]
		[ProducesResponseType(typeof(UserLocationPrivacyDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetMyUserLocationPrivacy(CancellationToken cancellationToken)
		{
			var result = await mUserLocationPrivacy.GetUserLocationPrivacyAsync(UserId, cancellationToken);
			if (result == null)
			{
				return NotFound(new { message = "User location privacy not found." });
			}
			return Ok(result);
		}

		[HttpPatch("/api/users/me/location-privacy")]
		[ProducesResponseType(typeof(UserLocationPrivacyDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> PatchUserLocationPrivacy([FromBody] UserLocationPrivacyPatchDto patch, CancellationToken cancellationToken)
		{
			var updated = await mUserLocationPrivacy.PatchUserLocationPrivacyAsync(UserId, patch, cancellationToken);
			if (updated == null)
			{
				return NotFound(new { message = "User location privacy not found." });
			}
			return Ok(updated);
		}

		private readonly IUserLocationPrivacyService mUserLocationPrivacy;
	}
}
