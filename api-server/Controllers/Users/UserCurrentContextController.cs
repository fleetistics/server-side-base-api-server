using api_server.Controllers.Base;
using api_server.Controllers.Users.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Users
{
	public class UserCurrentContextController : AuthAPIController
	{
		public UserCurrentContextController(IUserCurrentContextService userCurrentContext)
		{
			mUserCurrentContext = userCurrentContext;
		}

		[HttpGet("/api/users/me/active-team-id")]
		[ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
		public async Task<IActionResult> GetMyActiveTeamId(CancellationToken cancellationToken)
		{
			var teamId = await mUserCurrentContext.GetMyActiveTeamIdAsync(UserId, cancellationToken);
			return Ok(teamId);
		}

		private readonly IUserCurrentContextService mUserCurrentContext;
	}
}
