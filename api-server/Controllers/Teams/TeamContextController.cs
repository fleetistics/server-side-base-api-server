using api_server.Controllers.Base;
using api_server.Controllers.Teams.Dto;
using api_server.Controllers.Teams.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Teams
{
	public class TeamContextController : AuthAPIController
	{
		public TeamContextController(ITeamContextService teamContextService)
		{
			mTeamContextService = teamContextService;
		}

		[HttpGet("/api/team/{teamId}/context")]
		[ProducesResponseType(typeof(TeamContextDelta), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status410Gone)]
		public async Task<IActionResult> GetTeamContext(int teamId, CancellationToken cancellationToken)
		{
			var result = await mTeamContextService.GetTeamContextAsync(UserId, teamId, cancellationToken);
			return toActionResult(result);
		}

		[HttpGet("/api/team/{teamId}/context/delta")]
		[ProducesResponseType(typeof(TeamContextDelta), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status410Gone)]
		public async Task<IActionResult> GetTeamContextDelta(int teamId, [FromQuery] long? latestUpdateDate, CancellationToken cancellationToken)
		{
			// long (non-nullable) model binding for a missing query value silently falls back to
			// 0 rather than failing - it doesn't trigger [ApiController]'s automatic 400 the way
			// a genuine type-mismatch (e.g. ?latestUpdateDate=abc) does. Binding to long? instead
			// makes "omitted" distinguishable from "explicitly 0" so this can be rejected on purpose.
			if (latestUpdateDate is null)
				return BadRequest(new { message = "latestUpdateDate is required." });

			var result = await mTeamContextService.GetTeamContextDeltaAsync(UserId, teamId, latestUpdateDate.Value, cancellationToken);
			return toActionResult(result);
		}

		private IActionResult toActionResult(TeamContextUpdateResult result) => result.Outcome switch
		{
			TeamContextUpdateOutcome.TeamNotFound => StatusCode(StatusCodes.Status410Gone),
			TeamContextUpdateOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
			TeamContextUpdateOutcome.NotModified => NoContent(),
			_ => Ok(result.Delta)
		};

		private readonly ITeamContextService mTeamContextService;
	}
}
