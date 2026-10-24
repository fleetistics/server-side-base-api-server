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

		[HttpPost("/api/team")]
		[ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		public async Task<IActionResult> CreateTeam([FromBody] TeamCreatePatchDto body, CancellationToken cancellationToken)
		{
			// A new team needs a name; on PATCH an omitted name just means "unchanged", so this is
			// enforced here rather than in TeamCreatePatchDto (shared by both). A blank name is
			// rejected by TeamPatchDto.Validate.
			if (body.Team?.TeamName is not { IsSet: true, Value: not null })
			{
				ModelState.AddModelError("Team.TeamName", "TeamName is required.");
				return ValidationProblem(ModelState);
			}

			var teamId = await mTeamService.CreateTeamAsync(UserId, body, cancellationToken);
			return CreatedAtAction(nameof(GetCreatingTeam), null, teamId);
		}

		[HttpPatch("/api/team/{teamId}")]
		[ProducesResponseType(typeof(TeamCreateDto), StatusCodes.Status200OK)]
		public async Task<IActionResult> PatchCreatingTeam(int teamId, [FromBody] TeamCreatePatchDto body, CancellationToken cancellationToken)
		{
			await mTeamService.PatchCreateTeamAsync(UserId, teamId, body, cancellationToken);
			return Ok(await mTeamService.GetCreatingTeamByUserAsync(UserId, cancellationToken));
		}

		[HttpPost("/api/team/{teamId}/start")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		public async Task<IActionResult> StartTeam(int teamId, CancellationToken cancellationToken)
		{
			await mTeamService.StartTeamAsync(UserId, teamId, cancellationToken);
			return NoContent();
		}

		private readonly ITeamService mTeamService;
	}
}
