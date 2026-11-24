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

		/// <summary>How many days the code works; left out means the configured default.</summary>
		public int? ExpiresInDays { get; set; }

		[MaxLength(1000)]
		public string? Message { get; set; }

		/// <summary>True (the default): an admin has to approve the person after they enter the code.</summary>
		public bool RequireAdminApproval { get; set; } = true;
	}

	/// <summary>One personal invitation of the company, without its code (only a hash of it is stored).</summary>
	public class JoinInvitationRowDto
	{
		public int Id { get; set; }
		public string? Email { get; set; }
		public string? Name { get; set; }
		public string? Message { get; set; }
		public int? PermissionRoleId { get; set; }
		public int? GroupId { get; set; }
		public bool RequireAdminApproval { get; set; }
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

		/// <summary>The new code in plain text. It is shown once here and is in the email; the server keeps only a hash.</summary>
		public string JoinCode { get; set; } = default!;

		/// <summary>"Sent" (queued for delivery), "NoTemplate" (no join-invitation template yet), "NotAvailable" (email transport is off) or "Failed".</summary>
		public string EmailState { get; set; } = default!;
	}

	/// <summary>The company's current join code: one code for one person, made when the page opens.</summary>
	public class CompanyJoinCodeDto
	{
		public int Id { get; set; }
		public string JoinCode { get; set; } = default!;
		public DateTime ExpirationDate { get; set; }
		public int? PermissionRoleId { get; set; }
		public int? GroupId { get; set; }
		public bool RequireAdminApproval { get; set; }
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
		/// <summary>True when the person came with the company code, false when with a personal invitation.</summary>
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
	}

	/// <summary>What the accept page shows about an invitation.</summary>
	public class JoinInfoDto
	{
		/// <summary>Set when the invitation is a personal one (the page was opened from the email link); null for the company code.</summary>
		public int? InvitationId { get; set; }
		public string CompanyName { get; set; } = default!;
		public string? InviterName { get; set; }
		public string? InviteeName { get; set; }
		public string? Message { get; set; }
		public bool RequiresApproval { get; set; }
		public DateTime ExpirationDate { get; set; }
	}

	public class JoinAcceptedDto
	{
		/// <summary>"Joined" (the person is a member now) or "PendingApproval" (an admin has to approve first).</summary>
		public string Result { get; set; } = default!;
		public string CompanyName { get; set; } = default!;
	}
}
