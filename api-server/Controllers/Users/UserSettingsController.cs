using api_server.Controllers.Base;
using api_server.Controllers.Users.Dto;
using api_server.Controllers.Users.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Users
{
	public class UserSettingsController : AuthAPIController
	{
		public UserSettingsController(IUserSettingsService userSettings)
		{
			mUserSettings = userSettings;
		}

		[HttpGet("/api/users/me/settings")]
		[ProducesResponseType(typeof(List<UserSettingsDto>), StatusCodes.Status200OK)]
		public async Task<IActionResult> GetUserSettings([FromQuery] short clientApplicationId, [FromQuery] short clientDevicePlatformId, CancellationToken cancellationToken)
		{
			var result = await mUserSettings.GetUserSettingsAsync(UserId, clientApplicationId, clientDevicePlatformId, cancellationToken);
			return Ok(result);
		}

		[HttpPut("/api/users/me/settings")]
		[ProducesResponseType(typeof(UserSettingsDto), StatusCodes.Status200OK)]
		public async Task<IActionResult> PutUserSetting([FromBody] UserSettingsPutDto request, CancellationToken cancellationToken)
		{
			var result = await mUserSettings.PutUserSettingAsync(UserId, request.Name, request.Value, request.ClientApplicationId, request.ClientDevicePlatformId, cancellationToken);
			return Ok(result);
		}

		private readonly IUserSettingsService mUserSettings;
	}
}
