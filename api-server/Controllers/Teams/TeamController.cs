using api_server.Controllers.Base;
using api_server.Controllers.Teams.Services;
using api_server.Teams;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Teams
{
	public class TeamController : AuthAPIController
	{
		public TeamController(ITeamService teamService)
		{
			mTeamService = teamService;
		}

		/// <summary>Id of the current user's active team; 204 when they have none.</summary>
		[HttpGet("/api/team/active")]
		[ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		public async Task<IActionResult> GetActiveTeamId(CancellationToken cancellationToken)
		{
			var teamId = await mTeamService.GetUserActiveTeamIdAsync(UserId, cancellationToken);
			if (teamId == null) return NoContent();
			return Ok(teamId.Value);
		}

		/// <summary>The team the current user is still setting up; 204 when there is none.</summary>
		[HttpGet("/api/team/creating")]
		[ProducesResponseType(typeof(TeamCreateDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		public async Task<IActionResult> GetCreatingTeam(CancellationToken cancellationToken)
		{
			var team = await mTeamService.GetCreatingTeamByUserAsync(UserId, cancellationToken);
			if (team == null) return NoContent();
			return Ok(team);
		}

		private readonly ITeamService mTeamService;
	}
}
