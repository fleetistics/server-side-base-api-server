using api_server.Controllers.Base;

using Microsoft.AspNetCore.Mvc;

namespace api_server.Missions
{
	public class MissionController : AuthAPIController
	{
		public MissionController(IMissionService missionService)
		{
			mMissionService = missionService;
		}

		///// <summary>Id of the current user's active mission; 204 when they have none.</summary>
		//[HttpGet("/api/mission/active")]
		//[ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
		//[ProducesResponseType(StatusCodes.Status204NoContent)]
		//public async Task<IActionResult> GetActiveMissionId(CancellationToken cancellationToken)
		//{
		//	var missionId = await mMissionService.GetUserActiveMissionIdAsync(UserId, cancellationToken);
		//	if (missionId == null) return NoContent();
		//	return Ok(missionId.Value);
		//}

		/// <summary>The mission the current user is still setting up; 204 when there is none.</summary>
		[HttpGet("/api/mission/creating")]
		[ProducesResponseType(typeof(CreatingMissionDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		public async Task<IActionResult> GetCreatingMission(CancellationToken cancellationToken)
		{
			var mission = await mMissionService.GetCreatingMissionByUserAsync(UserId, cancellationToken);
			if (mission == null) return NoContent();
			return Ok(mission);
		}

		//[HttpGet("/api/mission/{missionId}")]
		//[ProducesResponseType(typeof(MissionDto), StatusCodes.Status200OK)]
		//[ProducesResponseType(StatusCodes.Status404NotFound)]
		//public async Task<IActionResult> GetMission(int missionId, CancellationToken cancellationToken)
		//{
		//	var mission = await mMissionService.GetMissionAsync(missionId, cancellationToken);
		//	if (mission == null) return NotFound();
		//	return Ok(mission);
		//}

		//[HttpGet("/api/mission/{missionId}/details")]
		//[ProducesResponseType(typeof(MissionDetailsDto), StatusCodes.Status200OK)]
		//[ProducesResponseType(StatusCodes.Status204NoContent)]
		//public async Task<IActionResult> GetMissionDetails(int missionId, CancellationToken cancellationToken)
		//{
		//	var details = await mMissionService.GetMissionDetailsAsync(missionId, cancellationToken);
		//	if (details == null) return NoContent();
		//	return Ok(details);
		//}

		//[HttpGet("/api/mission/{missionId}/primary-target-user")]
		//[ProducesResponseType(typeof(MissionPrimaryTargetUserDto), StatusCodes.Status200OK)]
		//[ProducesResponseType(StatusCodes.Status204NoContent)]
		//public async Task<IActionResult> GetPrimaryTargetUser(int missionId, CancellationToken cancellationToken)
		//{
		//	var target = await mMissionService.GetPrimaryTargetUserAsync(missionId, cancellationToken);
		//	if (target == null) return NoContent();
		//	return Ok(target);
		//}

		[HttpPost("/api/mission")]
		[ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		public async Task<IActionResult> CreateMission([FromBody] MissionCreatePatchDto body, CancellationToken cancellationToken)
		{
			// A new mission needs a name; on PATCH an omitted name just means "unchanged", so this is
			// enforced here rather than in MissionCreatePatchDto (shared by both). A blank name is
			// rejected by MissionPatchDto.Validate.
			if (body.Mission?.Name is not { IsSet: true, Value: not null })
			{
				ModelState.AddModelError("Mission.Name", "Name is required.");
				return ValidationProblem(ModelState);
			}

			var missionId = await mMissionService.CreateMissionAsync(UserId, body, cancellationToken);
			return CreatedAtAction(nameof(GetCreatingMission), null, missionId);
		}

		/// <summary>Assigns the mission's primary target user (one record per mission; an existing one is replaced).</summary>
		[HttpPut("/api/mission/{missionId}/primary-target-user")]
		[ProducesResponseType(typeof(MissionPrimaryTargetUserDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		public async Task<IActionResult> AssignPrimaryTargetUser(int missionId, [FromBody] MissionPrimaryTargetUserAssignDto body, CancellationToken cancellationToken)
		{
			var result = await mMissionService.AssignPrimaryTargetUserAsync(UserId, missionId, body.TargetUserId!.Value, cancellationToken);
			return Ok(result);
		}

		[HttpPatch("/api/mission/{missionId}")]
		[ProducesResponseType(typeof(CreatingMissionDto), StatusCodes.Status200OK)]
		public async Task<IActionResult> PatchCreatingMission(int missionId, [FromBody] MissionCreatePatchDto body, CancellationToken cancellationToken)
		{
			await mMissionService.PatchCreateMissionAsync(UserId, missionId, body, cancellationToken);
			return Ok(await mMissionService.GetCreatingMissionByUserAsync(UserId, cancellationToken));
		}

		[HttpPost("/api/mission/{missionId}/start")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		public async Task<IActionResult> StartMission(int missionId, CancellationToken cancellationToken)
		{
			await mMissionService.StartMissionAsync(UserId, missionId, cancellationToken);
			return NoContent();
		}

		private readonly IMissionService mMissionService;
	}
}
