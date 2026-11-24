using System.ComponentModel.DataAnnotations;

namespace api_server.Controllers.Join.Dto
{
	/// <summary>Invites one person to the caller's company.</summary>
	public class InviteUserDto
	{
		[Required, MaxLength(320)]
		public string Email { get; set; } = default!;

		[MaxLength(200)]
		public string? Name { get; set; }

		/// <summary>A system role or one of the company's. Left out: the company's default role is used when the person is let in.</summary>
		public int? PermissionRoleId { get; set; }

		public int? GroupId { get; set; }

		[MaxLength(1000)]
		public string? Message { get; set; }

		/// <summary>True (the default): an admin has to approve the person after they enter the code.</summary>
		public bool RequireAdminApproval { get; set; } = true;
	}

	/// <summary>One invitation to a person (a User or Personal one; the company's own code is not in the list).</summary>
	public class JoinInvitationRowDto
	{
		public int Id { get; set; }
		public string? Email { get; set; }
		public string? Name { get; set; }
		public string? Message { get; set; }
		public int? PermissionRoleId { get; set; }
		public int? GroupId { get; set; }
		public bool RequireAdminApproval { get; set; }
		/// <summary>A UserJoinInvitationType id: 2 User, 3 Personal.</summary>
		public short TypeId { get; set; }
		/// <summary>A UserJoinInvitationStatus id: 1 Active, 2 Expired, 3 Accepted, 4 Cancelled. An Active one past its expiry is <see cref="IsExpired"/>.</summary>
		public short StatusId { get; set; }
		public bool IsExpired { get; set; }
		public DateTime CreationDate { get; set; }
		public DateTime ExpirationDate { get; set; }
		public int CreatedByUserId { get; set; }
		public string? CreatedByName { get; set; }
	}

	/// <summary>What sending (or resending) an invitation did.</summary>
	public class InvitationResultDto
	{
		public JoinInvitationRowDto Invitation { get; set; } = default!;

		/// <summary>The code the person has to enter (the company's code for a User invitation). Shown to the admin so they can pass it on when the email did not go out.</summary>
		public string JoinCode { get; set; } = default!;

		/// <summary>"Sent" (queued for delivery), "NoTemplate" (no join-invitation template yet), "NotAvailable" (email transport is off) or "Failed".</summary>
		public string EmailState { get; set; } = default!;
	}

	/// <summary>The company's join code: the one anonymous row, which stays until an admin regenerates it.</summary>
	public class CompanyJoinCodeDto
	{
		public int Id { get; set; }
		public string JoinCode { get; set; } = default!;
		/// <summary>False when an admin switched joining with the code off ("Allow people to request access with a code" is unticked).</summary>
		public bool Enabled { get; set; }
		public int? PermissionRoleId { get; set; }
		public int? GroupId { get; set; }
		public bool RequireAdminApproval { get; set; }
		/// <summary>How many invitations to people are open and would stop working if the code was regenerated (their email has the old code).</summary>
		public int OpenInvitations { get; set; }
	}

	/// <summary>Regenerates the company code and sets what people who join with it get. All three replace the current values.</summary>
	public class RegenerateCompanyJoinCodeDto
	{
		public int? PermissionRoleId { get; set; }
		public int? GroupId { get; set; }
		public bool RequireAdminApproval { get; set; } = true;
	}

	public class SetCompanyJoinCodeEnabledDto
	{
		public bool Enabled { get; set; }
	}

	/// <summary>What people who join with the company code get. All three replace the current values.</summary>
	public class UpdateCompanyJoinCodeDto
	{
		public int? PermissionRoleId { get; set; }
		public int? GroupId { get; set; }
		public bool RequireAdminApproval { get; set; } = true;
	}

	/// <summary>One request to join the company, with the person's name and address.</summary>
	public class JoinRequestRowDto
	{
		public int Id { get; set; }
		public int UserId { get; set; }
		public string? UserName { get; set; }
		public string? Email { get; set; }
		/// <summary>The note of the invitation that was used, when it had one.</summary>
		public string? Message { get; set; }
		public int InvitationId { get; set; }
		/// <summary>True when the person came with the company link, false when with an invitation to them.</summary>
		public bool ViaCompanyCode { get; set; }
		/// <summary>A UserJoinRequestStatus id: 1 waiting for an admin, 2 let in automatically, 3 approved, 4 denied, 5 cancelled.</summary>
		public short StatusId { get; set; }
		public DateTime CreationDate { get; set; }
		public int? PermissionRoleId { get; set; }
		public int? GroupId { get; set; }
	}

	public class JoinCodeRequestDto
	{
		[Required, MaxLength(32)]
		public string Code { get; set; } = default!;

		/// <summary>True for the company link (/join?company=1&amp;code=...): the code alone is enough and no invitation is needed. False: the person must have an open invitation to their email address.</summary>
		public bool CompanyLink { get; set; }
	}

	/// <summary>What the accept page shows about an invitation.</summary>
	public class JoinInfoDto
	{
		public string CompanyName { get; set; } = default!;
		public string? InviterName { get; set; }
		public string? InviteeName { get; set; }
		public string? Message { get; set; }
		public bool RequiresApproval { get; set; }
		/// <summary>When the invitation to the person ends; null for the company link, which does not expire.</summary>
		public DateTime? ExpirationDate { get; set; }
	}

	public class JoinAcceptedDto
	{
		/// <summary>"Joined" (the person is a member now) or "PendingApproval" (an admin has to approve first).</summary>
		public string Result { get; set; } = default!;
		public string CompanyName { get; set; } = default!;
	}
}
