using api_server.Controllers.Base;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Users
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
