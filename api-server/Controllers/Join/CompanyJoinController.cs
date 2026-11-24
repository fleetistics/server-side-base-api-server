using api_server.Controllers.Base;
using api_server.Controllers.Join.Dto;
using api_server.Controllers.Join.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Join
{
	/// <summary>
	/// The Admin ▸ User Management ▸ "Invites & Join" tab: personal invitations, the company's one-time join code, and the requests that
	/// come out of them. Everything belongs to the caller's company, which comes from the access token (403 without one).
	///
	/// NOTE: access is not restricted by role yet. Whoever can open the page can call these endpoints, and the menu only hides the page.
	/// Deciding who may invite and approve is still open.
	/// </summary>
	public class CompanyJoinController : AuthAPIController
	{
		public CompanyJoinController(ICompanyJoinService join)
		{
			mJoin = join;
		}

		// ---- personal invitations ----

		/// <summary>The company's personal invitations, newest first. Their codes are not in it: only a hash is kept.</summary>
		[HttpGet("/api/company/join/invitations")]
		[ProducesResponseType(typeof(IReadOnlyList<JoinInvitationRowDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetInvitations(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Ok(await mJoin.ListInvitationsAsync(companyId, cancellationToken));
		}

		/// <summary>
		/// Invites one person: stores the invitation (with a hash of a new one-time code) and emails them the code and the link to the accept
		/// page. Any open invitation to the same address is cancelled. The answer has the code in plain text, once, and says whether the
		/// email was queued. 409 when the address already belongs to a member.
		/// </summary>
		[HttpPost("/api/company/join/invitations")]
		[ProducesResponseType(typeof(InvitationResultDto), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> Invite([FromBody] InviteUserDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var (outcome, result) = await mJoin.InviteAsync(companyId, UserId, request, PortalBaseUrl(), cancellationToken);
			return outcome == JoinOutcome.Done ? Created($"/api/company/join/invitations/{result!.Invitation.Id}", result) : Failure(outcome);
		}

		/// <summary>Resends an invitation with a new code and a new expiry (as long as before). 409 for an accepted or cancelled one.</summary>
		[HttpPost("/api/company/join/invitations/{invitationId:int}/renew")]
		[ProducesResponseType(typeof(InvitationResultDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> Renew(int invitationId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var (outcome, result) = await mJoin.RenewAsync(companyId, invitationId, PortalBaseUrl(), cancellationToken);
			return outcome == JoinOutcome.Done ? Ok(result) : Failure(outcome);
		}

		/// <summary>Cancels an open invitation (or the company code). 409 for an accepted one.</summary>
		[HttpDelete("/api/company/join/invitations/{invitationId:int}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> Revoke(int invitationId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var outcome = await mJoin.RevokeAsync(companyId, invitationId, cancellationToken);
			return outcome == JoinOutcome.Done ? NoContent() : Failure(outcome);
		}

		// ---- the company code ----

		/// <summary>
		/// Makes a new one-time company join code and cancels the one from before, so call it when the page opens. The new code gets
		/// the role, group and approval setting of the old one. The answer has the code in plain text, once: it cannot be read back.
		/// </summary>
		[HttpPost("/api/company/join/company-code")]
		[ProducesResponseType(typeof(CompanyJoinCodeDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> NewCompanyCode(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Ok(await mJoin.NewCompanyCodeAsync(companyId, UserId, cancellationToken));
		}

		/// <summary>Sets what a person who joins with the current company code gets (replaces the three values). 409 when that code is no longer open.</summary>
		[HttpPut("/api/company/join/company-code/{invitationId:int}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> UpdateCompanyCode(int invitationId, [FromBody] UpdateCompanyJoinCodeDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var outcome = await mJoin.UpdateCompanyCodeAsync(companyId, invitationId, request, cancellationToken);
			return outcome == JoinOutcome.Done ? NoContent() : Failure(outcome);
		}

		// ---- requests ----

		/// <summary>Every request to join the company, newest first, with the person's name and address.</summary>
		[HttpGet("/api/company/join/requests")]
		[ProducesResponseType(typeof(IReadOnlyList<JoinRequestRowDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetRequests(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Ok(await mJoin.ListRequestsAsync(companyId, cancellationToken));
		}

		/// <summary>Lets the person in: makes them a member with the role and group of the invitation. 409 when the request is not waiting any more.</summary>
		[HttpPost("/api/company/join/requests/{requestId:int}/approve")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> Approve(int requestId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var outcome = await mJoin.ApproveAsync(companyId, UserId, requestId, cancellationToken);
			return outcome == JoinOutcome.Done ? NoContent() : Failure(outcome);
		}

		[HttpPost("/api/company/join/requests/{requestId:int}/deny")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> Deny(int requestId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var outcome = await mJoin.DenyAsync(companyId, UserId, requestId, cancellationToken);
			return outcome == JoinOutcome.Done ? NoContent() : Failure(outcome);
		}

		/// <summary>Where the portal is, for the link in the email, when Join:PortalBaseUrl is not set: the Origin of the browser that calls.</summary>
		private string? PortalBaseUrl() => Request.Headers.Origin.FirstOrDefault();

		private ObjectResult Failure(JoinOutcome outcome) => outcome switch
		{
			JoinOutcome.NotFound => Problem(title: "Not Found", detail: "There is no such invitation or request in this company.", statusCode: StatusCodes.Status404NotFound),
			JoinOutcome.InvalidEmail => Problem(title: "Invalid email", detail: "Enter a valid email address.", statusCode: StatusCodes.Status400BadRequest),
			JoinOutcome.InvalidExpiry => Problem(title: "Invalid expiry", detail: "The number of days is out of range.", statusCode: StatusCodes.Status400BadRequest),
			JoinOutcome.RoleNotAllowed => Problem(title: "Unknown role", detail: "The role is not one this company can use.", statusCode: StatusCodes.Status400BadRequest),
			JoinOutcome.GroupNotAllowed => Problem(title: "Unknown group", detail: "The group is not one of this company's.", statusCode: StatusCodes.Status400BadRequest),
			JoinOutcome.AlreadyMember => Problem(title: "Already a member", detail: "A user with this email address is already a member of the company.", statusCode: StatusCodes.Status409Conflict),
			JoinOutcome.WrongState => Problem(title: "Not possible now", detail: "The invitation or request is not in a state that allows this (it may be accepted, cancelled or handled already).", statusCode: StatusCodes.Status409Conflict),
			JoinOutcome.SelfNotAllowed => Problem(title: "Not your request", detail: "Nobody can approve their own request.", statusCode: StatusCodes.Status409Conflict),
			JoinOutcome.NoRole => Problem(title: "No role to give", detail: "The invitation has no role and the company has no default role. Pick a role on the invitation first.", statusCode: StatusCodes.Status409Conflict),
			JoinOutcome.MemberBlocked => Problem(title: "Person is suspended", detail: "The person is suspended in this company, so they cannot be let in by a request.", statusCode: StatusCodes.Status409Conflict),
			_ => throw new InvalidOperationException("Unhandled outcome"),
		};

		private readonly ICompanyJoinService mJoin;
	}
}
