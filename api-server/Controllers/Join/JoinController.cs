using api_server.Controllers.Base;
using api_server.Controllers.Join.Dto;
using api_server.Controllers.Join.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Join
{
	/// <summary>
	/// The accept page: the signed-in user opens the link (or types the code), sees which company it leads to, and accepts or denies.
	/// A link from an invitation email carries only the code (/join?code=...): the user must have an open invitation to their email
	/// address. The company link (/join?company=1&amp;code=...) is sent with <c>CompanyLink</c> and needs no invitation. Nothing here belongs
	/// to a company of the caller; the code is what proves the right. Wrong codes are counted per user and lock them out for a while (429).
	/// </summary>
	public class JoinController : AuthAPIController
	{
		public JoinController(IJoinAcceptService accept)
		{
			mAccept = accept;
		}

		/// <summary>Which company a code leads to, without using it.</summary>
		[HttpPost("/api/join/lookup")]
		[ProducesResponseType(typeof(JoinInfoDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
		public async Task<IActionResult> Lookup([FromBody] JoinCodeRequestDto request, CancellationToken cancellationToken)
		{
			var (outcome, info) = await mAccept.LookupAsync(UserId, request.Code, request.CompanyLink, cancellationToken);
			return outcome == AcceptOutcome.Done ? Ok(info) : Failure(outcome);
		}

		/// <summary>Uses the code. 200 says whether the person is a member now or an admin has to approve. An invitation to a person is used up; the company code stays. 409 when the person (or someone with their email address) is already a member.</summary>
		[HttpPost("/api/join/accept")]
		[ProducesResponseType(typeof(JoinAcceptedDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
		public async Task<IActionResult> Accept([FromBody] JoinCodeRequestDto request, CancellationToken cancellationToken)
		{
			var (outcome, result) = await mAccept.AcceptAsync(UserId, request.Code, request.CompanyLink, cancellationToken);
			return outcome == AcceptOutcome.Done ? Ok(result) : Failure(outcome);
		}

		/// <summary>Turns the person's invitation down; it is cancelled. The company link has no Deny, and the company code is left as it is.</summary>
		[HttpPost("/api/join/deny")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
		public async Task<IActionResult> Deny([FromBody] JoinCodeRequestDto request, CancellationToken cancellationToken)
		{
			var outcome = await mAccept.DenyAsync(UserId, request.Code, request.CompanyLink, cancellationToken);
			return outcome == AcceptOutcome.Done ? NoContent() : Failure(outcome);
		}

		private ObjectResult Failure(AcceptOutcome outcome) => outcome switch
		{
			AcceptOutcome.InvalidCode => Problem(title: "Invalid code", detail: "This code is not valid. Check it, or ask for a new invitation.", statusCode: StatusCodes.Status404NotFound),
			AcceptOutcome.Locked => Problem(title: "Too many attempts", detail: "Too many wrong codes. Try again in a few minutes.", statusCode: StatusCodes.Status429TooManyRequests),
			AcceptOutcome.NoInvitation => Problem(title: "No invitation", detail: "There is no open invitation to the email address of your account for this company. Ask for a new one, or use the company's join link.", statusCode: StatusCodes.Status404NotFound),
			AcceptOutcome.CodeDisabled => Problem(title: "Joining is switched off", detail: "This company does not accept people with a join link at the moment.", statusCode: StatusCodes.Status403Forbidden),
			AcceptOutcome.WrongAccount => Problem(title: "Another account", detail: "This invitation was sent to a different email address than the one of your account.", statusCode: StatusCodes.Status403Forbidden),
			AcceptOutcome.AlreadyMember => Problem(title: "Already a member", detail: "You are already a member of this company (your account or another one with your email address).", statusCode: StatusCodes.Status409Conflict),
			AcceptOutcome.NoRole => Problem(title: "Cannot join yet", detail: "The company has not set a role for new people. Ask them to fix the invitation.", statusCode: StatusCodes.Status409Conflict),
			AcceptOutcome.MemberBlocked => Problem(title: "Account suspended", detail: "Your account is suspended in this company.", statusCode: StatusCodes.Status403Forbidden),
			AcceptOutcome.NotFound => Problem(title: "Not Found", statusCode: StatusCodes.Status404NotFound),
			_ => throw new InvalidOperationException("Unhandled outcome"),
		};

		private readonly IJoinAcceptService mAccept;
	}
}
