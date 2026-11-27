using api_server.Services;
using api_server.Controllers.Join.Dto;
using db_model.Companies;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Join.Services
{
	public enum AcceptOutcome
	{
		Done,
		/// <summary>No open code is like this one (wrong, replaced, cancelled or expired). One answer for all, so a guess learns nothing.</summary>
		InvalidCode,
		/// <summary>Too many wrong codes lately; try again later.</summary>
		Locked,
		/// <summary>The code is the company's but the user has no open invitation to their email address (and did not come by the company link).</summary>
		NoInvitation,
		/// <summary>The company link is switched off.</summary>
		CodeDisabled,
		/// <summary>The code is right but the invitation was sent to another address.</summary>
		WrongAccount,
		/// <summary>The user, or another user with the same email address, is already a member of the company.</summary>
		AlreadyMember,
		NoRole,
		MemberBlocked,
		NotFound,
	}

	/// <summary>The invitee's side of joining: look at what a code leads to, accept or deny.</summary>
	public interface IJoinAcceptService
	{
		/// <summary>What the code leads to, without using it.</summary>
		Task<(AcceptOutcome Outcome, JoinInfoDto? Info)> LookupAsync(int userId, string code, bool companyLink, CancellationToken cancellationToken);

		/// <summary>Uses the code: makes the join request and, when no admin approval is needed, the membership.</summary>
		Task<(AcceptOutcome Outcome, JoinAcceptedDto? Result)> AcceptAsync(int userId, string code, bool companyLink, CancellationToken cancellationToken);

		/// <summary>Turns the user's invitation down (it is cancelled). The company code is left alone: it is not the person's to cancel.</summary>
		Task<AcceptOutcome> DenyAsync(int userId, string code, bool companyLink, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Which code is entered and from where decides what it opens:
	/// the company link (<c>companyLink</c>) works with the company's code alone and joins by the company row's role, group and approval;
	/// otherwise the code must be the company's (or a Personal invitation's own) and the user must have an open invitation to their email
	/// address, whose role, group and approval apply and which is used up.
	/// </summary>
	public sealed class JoinAcceptService : IJoinAcceptService
	{
		public JoinAcceptService(IRepository repository, JoinAttemptThrottle throttle, TimeProvider time)
		{
			mRepository = repository;
			mThrottle = throttle;
			mTime = time;
		}

		public async Task<(AcceptOutcome Outcome, JoinInfoDto? Info)> LookupAsync(int userId, string code, bool companyLink, CancellationToken cancellationToken)
		{
			var (outcome, used) = await ResolveAsync(userId, code, companyLink, track: false, cancellationToken);
			if (outcome != AcceptOutcome.Done) return (outcome, null);
			return (AcceptOutcome.Done, await InfoAsync(used!, cancellationToken));
		}

		public async Task<(AcceptOutcome Outcome, JoinAcceptedDto? Result)> AcceptAsync(int userId, string code, bool companyLink, CancellationToken cancellationToken)
		{
			var (outcome, used) = await ResolveAsync(userId, code, companyLink, track: true, cancellationToken);
			if (outcome != AcceptOutcome.Done) return (outcome, null);
			var invitation = used!;

			if (await IsMemberByEmailAsync(invitation.CompanyId, userId, cancellationToken)) return (AcceptOutcome.AlreadyMember, null);

			var needsApproval = invitation.DoesNotRequireAdminApproval != true;
			if (!needsApproval)
			{
				var grant = await JoinMembership.GrantAsync(mRepository, invitation.CompanyId, userId, invitation.PermissionRoleId, invitation.GroupId, cancellationToken);
				if (grant == MembershipGrant.NoRole) return (AcceptOutcome.NoRole, null);
				if (grant == MembershipGrant.Blocked) return (AcceptOutcome.MemberBlocked, null);
			}

			// A person with a request still waiting does not get a second one.
			var waiting = await mRepository.GetQueryable<UserJoinRequest>(r => r.CompanyId == invitation.CompanyId && r.UserId == userId && r.StatusId == UserJoinRequestStatus.RequestedByUser).AnyAsync(cancellationToken);
			if (!waiting)
			{
				mRepository.Create(new UserJoinRequest
				{
					CompanyId = invitation.CompanyId,
					UserJoinInvitationId = invitation.Id,
					UserId = userId,
					StatusId = needsApproval ? UserJoinRequestStatus.RequestedByUser : UserJoinRequestStatus.AutoAccepted,
					CreationDate = Now(),
					// Nobody handled it yet; the column cannot be empty, so it names whoever made the invitation.
					HandledByCompanyUserId = invitation.CreatedByUserId,
					LatestUpdate = Now(),
				});
			}

			// An invitation to a person is used up; the company's code stays for the next person.
			if (invitation.TypeId != UserJoinInvitationType.Company)
			{
				invitation.StatusId = UserJoinInvitationStatus.Accepted;
				invitation.UserId ??= userId;
				invitation.LatestUpdate = Now();
			}
			await mRepository.SaveAsync(cancellationToken);
			mThrottle.Reset(userId);

			var companyName = await mRepository.GetQueryable<Company>(c => c.Id == invitation.CompanyId).AsNoTracking().Select(c => c.Name).SingleAsync(cancellationToken);
			return (AcceptOutcome.Done, new JoinAcceptedDto { Result = needsApproval ? "PendingApproval" : "Joined", CompanyName = companyName });
		}

		public async Task<AcceptOutcome> DenyAsync(int userId, string code, bool companyLink, CancellationToken cancellationToken)
		{
			var (outcome, used) = await ResolveAsync(userId, code, companyLink, track: true, cancellationToken);
			if (outcome != AcceptOutcome.Done) return outcome;

			if (used!.TypeId != UserJoinInvitationType.Company)
			{
				used.StatusId = UserJoinInvitationStatus.Cancelled;
				used.LatestUpdate = Now();
				await mRepository.SaveAsync(cancellationToken);
			}
			mThrottle.Reset(userId);
			return AcceptOutcome.Done;
		}

		// ---- helpers ----

		private DateTime Now() => mTime.GetUtcNow().UtcDateTime;

		private bool IsOpen(UserJoinInvitation i) => i.StatusId == UserJoinInvitationStatus.Active && i.ExpirationDate > Now();

		/// <summary>
		/// The invitation the code opens for this user: the company row (company link), the user's own open invitation (the company's code
		/// with an invitation to their email) or a Personal invitation with this code. A wrong code counts against the user; a right one
		/// that does not fit them (another address, no invitation, switched off) does not.
		/// </summary>
		private async Task<(AcceptOutcome Outcome, UserJoinInvitation? Used)> ResolveAsync(int userId, string code, bool companyLink, bool track, CancellationToken cancellationToken)
		{
			if (mThrottle.IsLocked(userId)) return (AcceptOutcome.Locked, null);

			var formatted = JoinCodes.Format(code);
			UserJoinInvitation? row = null;
			if (formatted != null)
			{
				var query = mRepository.GetQueryable<UserJoinInvitation>(i => i.JoinCode == formatted);
				row = await (track ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
			}

			var usable = row != null && (row.TypeId == UserJoinInvitationType.Company
				? row.StatusId is UserJoinInvitationStatus.Active or UserJoinInvitationStatus.Disabled
				: !companyLink && IsOpen(row));
			if (!usable)
			{
				mThrottle.RecordFailure(userId);
				return (AcceptOutcome.InvalidCode, null);
			}
			row = row!;

			if (row.TypeId == UserJoinInvitationType.Personal)
				return await IsInviteeAsync(userId, row, cancellationToken) ? (AcceptOutcome.Done, row) : (AcceptOutcome.WrongAccount, null);

			if (companyLink)
				return row.StatusId == UserJoinInvitationStatus.Disabled ? (AcceptOutcome.CodeDisabled, null) : (AcceptOutcome.Done, row);

			var invitation = await FindInvitationForAsync(userId, row.CompanyId, track, cancellationToken);
			return invitation == null ? (AcceptOutcome.NoInvitation, null) : (AcceptOutcome.Done, invitation);
		}

		/// <summary>The user's newest open invitation in the company: the one they were matched to when it was sent, or one sent to their address.</summary>
		private async Task<UserJoinInvitation?> FindInvitationForAsync(int userId, int companyId, bool track, CancellationToken cancellationToken)
		{
			var email = await mRepository.GetQueryable<User>(u => u.Id == userId).AsNoTracking().Select(u => u.Email).SingleOrDefaultAsync(cancellationToken);
			var now = Now();
			var query = mRepository.GetQueryable<UserJoinInvitation>(i => i.CompanyId == companyId && i.TypeId == UserJoinInvitationType.User
				&& i.StatusId == UserJoinInvitationStatus.Active && i.ExpirationDate > now);
			var open = await (track ? query : query.AsNoTracking()).OrderByDescending(i => i.Id).ToListAsync(cancellationToken);
			return open.FirstOrDefault(i => i.UserId == userId || JoinMembership.EmailMatches(email, i.Email));
		}

		/// <summary>True for the user a Personal invitation was sent to: the one it was matched to when sent, or whoever has its address.</summary>
		private async Task<bool> IsInviteeAsync(int userId, UserJoinInvitation invitation, CancellationToken cancellationToken)
		{
			if (invitation.UserId == userId) return true;
			var email = await mRepository.GetQueryable<User>(u => u.Id == userId).AsNoTracking().Select(u => u.Email).SingleOrDefaultAsync(cancellationToken);
			return JoinMembership.EmailMatches(email, invitation.Email);
		}

		/// <summary>True when the user, or any user with the same email address, is a member of the company (emails are not unique, so the address counts, not only the account).</summary>
		private async Task<bool> IsMemberByEmailAsync(int companyId, int userId, CancellationToken cancellationToken)
		{
			var email = await mRepository.GetQueryable<User>(u => u.Id == userId).AsNoTracking().Select(u => u.Email).SingleOrDefaultAsync(cancellationToken);
			var ids = new List<int> { userId };
			if (!string.IsNullOrWhiteSpace(email))
			{
				var lower = email.Trim().ToLower();
				ids.AddRange(await mRepository.GetQueryable<User>(u => u.KindId != UserKind.Contact && u.Email != null && u.Email.ToLower() == lower).AsNoTracking().Select(u => u.Id).ToListAsync(cancellationToken));
			}
			var statuses = await mRepository.GetQueryable<User2Company>(m => m.CompanyId == companyId && ids.Contains(m.UserId)).AsNoTracking().Select(m => m.StatusId).ToListAsync(cancellationToken);
			return statuses.Any(JoinMembership.IsMember);
		}

		private async Task<JoinInfoDto> InfoAsync(UserJoinInvitation invitation, CancellationToken cancellationToken)
		{
			var companyName = await mRepository.GetQueryable<Company>(c => c.Id == invitation.CompanyId).AsNoTracking().Select(c => c.Name).SingleAsync(cancellationToken);
			var inviter = await mRepository.GetQueryable<User>(u => u.Id == invitation.CreatedByUserId).AsNoTracking().Select(u => new { u.DisplayName, u.FirstName, u.LastName, u.UserName }).SingleOrDefaultAsync(cancellationToken);
			var isCompany = invitation.TypeId == UserJoinInvitationType.Company;
			return new JoinInfoDto
			{
				CompanyName = companyName,
				InviterName = inviter == null ? null : new[] { inviter.DisplayName, UserNameHelper.Compose(inviter.FirstName, inviter.LastName), inviter.UserName }.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
				InviteeName = invitation.Name,
				Message = invitation.Message,
				RequiresApproval = invitation.DoesNotRequireAdminApproval != true,
				ExpirationDate = isCompany ? null : invitation.ExpirationDate,
			};
		}

		private readonly IRepository mRepository;
		private readonly JoinAttemptThrottle mThrottle;
		private readonly TimeProvider mTime;
	}
}
