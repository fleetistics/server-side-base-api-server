using api_server.Controllers.Base;
using api_server.Controllers.Users.Dto;
using api_server.Controllers.Users.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Users
{
	public class UserEmergencyController : AuthAPIController
	{
		public UserEmergencyController(IUserEmergencyAlertService userEmergencyAlert)
		{
			mUserEmergencyAlert = userEmergencyAlert;
		}

		[HttpPost("/api/users/me/emergency-alert")]
		[ProducesResponseType(typeof(UserEmergencyAlertDto), StatusCodes.Status201Created)]
		public async Task<IActionResult> CreateMyEmergencyAlert([FromBody] CreateUserEmergencyAlertDto body, CancellationToken cancellationToken)
		{
			var alert = await mUserEmergencyAlert.CreateMyEmergencyAlertAsync(UserId, body, cancellationToken);
			return CreatedAtAction(nameof(GetMyActiveEmergencyAlert), null, alert);
		}

		[HttpGet("/api/users/me/active-emergency-alert")]
		[ProducesResponseType(typeof(MyUserEmergencyAlertDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetMyActiveEmergencyAlert(CancellationToken cancellationToken)
		{
			var alert = await mUserEmergencyAlert.GetMyActiveEmergencyAlertAsync(UserId, cancellationToken);
			if (alert == null)
			{
				return NotFound(new { message = "No active emergency alert." });
			}
			return Ok(alert);
		}

		[HttpPost("/api/users/me/emergency-alert/complete")]
		[ProducesResponseType(typeof(UserEmergencyAlertDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> CompleteMyEmergencyAlert([FromBody] CompleteUserEmergencyAlertDto body, CancellationToken cancellationToken)
		{
			var alert = await mUserEmergencyAlert.CompleteMyEmergencyAlertAsync(UserId, body, cancellationToken);
			if (alert == null)
			{
				return NotFound(new { message = "No active emergency alert." });
			}
			return Ok(alert);
		}

		private readonly IUserEmergencyAlertService mUserEmergencyAlert;
	}
}
