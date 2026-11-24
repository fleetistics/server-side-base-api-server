using api_server.Controllers.Base;
using api_server.Controllers.Join.Dto;
using api_server.Controllers.Join.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Join
{
	/// <summary>
	/// The accept-invitation page: the signed-in user enters the code from the email (or from the company's join code), sees which
	/// company it leads to, and accepts or denies. Nothing here belongs to a company of the caller; the code is what proves the right.
	/// Wrong codes are counted per user and lock them out for a while (429).
	/// </summary>
	public class JoinController : AuthAPIController
	{
		public JoinController(IJoinAcceptService accept)
		{
			mAccept = accept;
		}

		/// <summary>
		/// What the page opened from the email link shows. 404 unless the invitation is open and was sent to the signed-in user, so
		/// nobody else learns which company it is.
		/// </summary>
		[HttpGet("/api/join/invitations/{invitationId:int}")]
		[ProducesResponseType(typeof(JoinInfoDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetInvitation(int invitationId, CancellationToken cancellationToken)
		{
			var info = await mAccept.GetInvitationInfoAsync(UserId, invitationId, cancellationToken);
			return info == null ? Problem(title: "Not Found", detail: "This invitation is not open for you.", statusCode: StatusCodes.Status404NotFound) : Ok(info);
		}

		/// <summary>Which company a code leads to, without using it.</summary>
		[HttpPost("/api/join/lookup")]
		[ProducesResponseType(typeof(JoinInfoDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
		public async Task<IActionResult> Lookup([FromBody] JoinCodeRequestDto request, CancellationToken cancellationToken)
		{
			var (outcome, info) = await mAccept.LookupAsync(UserId, request.Code, cancellationToken);
			return outcome == AcceptOutcome.Done ? Ok(info) : Failure(outcome);
		}

		/// <summary>Uses the code. 200 says whether the person is a member now or an admin has to approve; the code is spent.</summary>
		[HttpPost("/api/join/accept")]
		[ProducesResponseType(typeof(JoinAcceptedDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
		public async Task<IActionResult> Accept([FromBody] JoinCodeRequestDto request, CancellationToken cancellationToken)
		{
			var (outcome, result) = await mAccept.AcceptAsync(UserId, request.Code, cancellationToken);
			return outcome == AcceptOutcome.Done ? Ok(result) : Failure(outcome);
		}

		/// <summary>Turns a personal invitation down; it is cancelled. The company code is left as it is.</summary>
		[HttpPost("/api/join/deny")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
		public async Task<IActionResult> Deny([FromBody] JoinCodeRequestDto request, CancellationToken cancellationToken)
		{
			var outcome = await mAccept.DenyAsync(UserId, request.Code, cancellationToken);
			return outcome == AcceptOutcome.Done ? NoContent() : Failure(outcome);
		}

		private ObjectResult Failure(AcceptOutcome outcome) => outcome switch
		{
			AcceptOutcome.InvalidCode => Problem(title: "Invalid code", detail: "This code is not valid. Check it, or ask for a new invitation.", statusCode: StatusCodes.Status404NotFound),
			AcceptOutcome.Locked => Problem(title: "Too many attempts", detail: "Too many wrong codes. Try again in a few minutes.", statusCode: StatusCodes.Status429TooManyRequests),
			AcceptOutcome.WrongAccount => Problem(title: "Another account", detail: "This invitation was sent to a different email address than the one of your account.", statusCode: StatusCodes.Status403Forbidden),
			AcceptOutcome.AlreadyMember => Problem(title: "Already a member", detail: "You are already a member of this company.", statusCode: StatusCodes.Status409Conflict),
			AcceptOutcome.NoRole => Problem(title: "Cannot join yet", detail: "The company has not set a role for new people. Ask them to fix the invitation.", statusCode: StatusCodes.Status409Conflict),
			AcceptOutcome.MemberBlocked => Problem(title: "Account suspended", detail: "Your account is suspended in this company.", statusCode: StatusCodes.Status403Forbidden),
			AcceptOutcome.NotFound => Problem(title: "Not Found", statusCode: StatusCodes.Status404NotFound),
			_ => throw new InvalidOperationException("Unhandled outcome"),
		};

		private readonly IJoinAcceptService mAccept;
	}
}
