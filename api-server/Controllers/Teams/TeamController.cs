using api_server.Controllers.Base;
using api_server.Controllers.Teams.Dto;
using api_server.Controllers.Teams.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Teams
{
	public class TeamController : AuthAPIController
	{
		public TeamController(ITeamService teamService)
		{
			mTeamService = teamService;
		}

		[HttpPost("/api/team")]
		[ProducesResponseType(typeof(TeamDto), StatusCodes.Status201Created)]
		public async Task<IActionResult> CreateTeam([FromBody] CreateTeamDto body, CancellationToken cancellationToken)
		{
			var team = await mTeamService.CreateTeamAsync(UserId, body, cancellationToken);
			return CreatedAtAction(nameof(GetTeam), new { teamId = team.Id }, team);
		}

		[HttpGet("/api/team/{teamId}")]
		[ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetTeam(int teamId, CancellationToken cancellationToken)
		{
			var team = await mTeamService.GetTeamAsync(teamId, cancellationToken);
			if (team == null)
			{
				return NotFound(new { message = "Team not found." });
			}
			return Ok(team);
		}

		[HttpPatch("/api/team/{teamId}")]
		[ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> PatchTeam(int teamId, [FromBody] TeamPatchDto patch, CancellationToken cancellationToken)
		{
			var team = await mTeamService.PatchTeamAsync(teamId, patch, cancellationToken);
			if (team == null)
			{
				return NotFound(new { message = "Team not found." });
			}
			return Ok(team);
		}

		[HttpGet("/api/team/{teamId}/details")]
		[ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetTeamDetails(int teamId, CancellationToken cancellationToken)
		{
			var details = await mTeamService.GetTeamDetailsAsync(teamId, cancellationToken);
			if (details == null)
			{
				return NotFound(new { message = "Team not found." });
			}
			return Ok(details);
		}

		[HttpPatch("/api/team/{teamId}/details")]
		[ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> PatchTeamDetails(int teamId, [FromBody] TeamPatchDto patch, CancellationToken cancellationToken)
		{
			var details = await mTeamService.PatchTeamDetailsAsync(teamId, patch, cancellationToken);
			if (details == null)
			{
				return NotFound(new { message = "Team not found." });
			}
			return Ok(details);
		}

		private readonly ITeamService mTeamService;
	}
}
