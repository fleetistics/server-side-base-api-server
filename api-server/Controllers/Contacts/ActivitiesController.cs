using api_server.Controllers.Base;
using api_server.Controllers.Contacts.Dto;
using api_server.Controllers.Contacts.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Contacts
{
	/// <summary>
	/// Contacts ▸ Activities: calls, meetings, emails and visits with the caller's companies and people, logged after they happened or
	/// scheduled for later (and optionally recurring). Everything belongs to the caller's company, which comes from the access token (403 without one).
	/// An activity that is private shows only to the user it is private to; asking for another user's private one answers 404.
	///
	/// NOTE: access is not restricted by role yet (same as the rest of the company admin endpoints).
	/// </summary>
	public class ActivitiesController : AuthAPIController
	{
		public ActivitiesController(IActivitiesService activities)
		{
			mActivities = activities;
		}

		/// <summary>The lists the activity forms pick from (types, priorities, statuses, repeat units, people to assign to).</summary>
		[HttpGet("/api/contacts/activities/lookups")]
		[ProducesResponseType(typeof(ActivityLookupsDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetLookups(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Ok(await mActivities.GetLookupsAsync(companyId, cancellationToken));
		}

		/// <summary>
		/// The activities the caller can see, newest first. Filter by company or person (their activity history), period (<c>from</c> included, <c>to</c> excluded,
		/// Unix seconds), type, priority, status and creator.
		/// </summary>
		[HttpGet("/api/contacts/activities")]
		[ProducesResponseType(typeof(IReadOnlyList<ActivityRowDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetActivities([FromQuery] ActivityListQuery query, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Ok(await mActivities.ListAsync(companyId, UserId, query, cancellationToken));
		}

		[HttpGet("/api/contacts/activities/{activityId:int}")]
		[ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetActivity(int activityId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Answer(await mActivities.GetAsync(companyId, UserId, activityId, cancellationToken));
		}

		/// <summary>
		/// Logs an activity that happened (<c>Log</c> true: saved as Completed) or schedules one (Scheduled). With <c>Recurrence</c> the whole series is made
		/// (up to 100 occurrences); the first one is returned (201).
		/// </summary>
		[HttpPost("/api/contacts/activities")]
		[ProducesResponseType(typeof(ActivityDto), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> CreateActivity([FromBody] SaveActivityDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var result = await mActivities.CreateAsync(companyId, UserId, request, cancellationToken);
			return result.Outcome == ContactOutcome.Done ? Created($"/api/contacts/activities/{result.Value!.Id}", result.Value) : Failure(result);
		}

		/// <summary>
		/// Replaces the activity's details (send the whole form; <c>Log</c>, <c>Recurrence</c> and <c>IsPrivate</c> are ignored). <c>scope</c> 2 (ThisAndFollowing) also
		/// changes the later Scheduled occurrences of its series, moving them by the same time difference. A cancelled activity cannot be edited.
		/// </summary>
		[HttpPut("/api/contacts/activities/{activityId:int}")]
		[ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> UpdateActivity(int activityId, [FromBody] SaveActivityDto request, [FromQuery] ActivityScope scope = ActivityScope.This, CancellationToken cancellationToken = default)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Answer(await mActivities.UpdateAsync(companyId, UserId, activityId, request, scope, cancellationToken));
		}

		/// <summary>Marks a scheduled activity as done now.</summary>
		[HttpPost("/api/contacts/activities/{activityId:int}/complete")]
		[ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> Complete(int activityId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Answer(await mActivities.CompleteAsync(companyId, UserId, activityId, cancellationToken));
		}

		/// <summary>Cancels a scheduled activity (it stays in the history as Cancelled; activities are not deleted). <c>scope</c> 2 also cancels the later Scheduled ones of its series.</summary>
		[HttpPost("/api/contacts/activities/{activityId:int}/cancel")]
		[ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> Cancel(int activityId, [FromQuery] ActivityScope scope = ActivityScope.This, CancellationToken cancellationToken = default)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Answer(await mActivities.CancelAsync(companyId, UserId, activityId, scope, cancellationToken));
		}

		private IActionResult Answer(ContactResult<ActivityDto> result) =>
			result.Outcome == ContactOutcome.Done ? Ok(result.Value) : Failure(result);

		private ObjectResult Failure(ContactResult<ActivityDto> result) => result.Outcome switch
		{
			ContactOutcome.NotFound => Problem(title: "Not Found", detail: result.Message ?? "There is no such activity.", statusCode: StatusCodes.Status404NotFound),
			ContactOutcome.Invalid => Problem(title: "Invalid input", detail: result.Message, statusCode: StatusCodes.Status400BadRequest),
			ContactOutcome.NotAllowed => Problem(title: "Not allowed", detail: result.Message, statusCode: StatusCodes.Status403Forbidden),
			_ => throw new InvalidOperationException("Unhandled outcome"),
		};

		private readonly IActivitiesService mActivities;
	}
}
