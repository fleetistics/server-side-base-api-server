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
		/// <summary>No open invitation has this code (wrong, used, replaced, cancelled or expired). One answer for all, so a guess learns nothing.</summary>
		InvalidCode,
		/// <summary>Too many wrong codes lately; try again later.</summary>
		Locked,
		/// <summary>The code is right but the invitation was sent to another address.</summary>
		WrongAccount,
		AlreadyMember,
		NoRole,
		MemberBlocked,
		NotFound,
	}

	/// <summary>The invitee's side of joining: look at an invitation, enter its code, accept or deny.</summary>
	public interface IJoinAcceptService
	{
		/// <summary>What the page opened from the email link shows. Null unless the invitation is open and was sent to the signed-in user.</summary>
		Task<JoinInfoDto?> GetInvitationInfoAsync(int userId, int invitationId, CancellationToken cancellationToken);

		/// <summary>What the code leads to, without using it.</summary>
		Task<(AcceptOutcome Outcome, JoinInfoDto? Info)> LookupAsync(int userId, string code, CancellationToken cancellationToken);

		/// <summary>Uses the code: makes the join request and, when no admin approval is needed, the membership.</summary>
		Task<(AcceptOutcome Outcome, JoinAcceptedDto? Result)> AcceptAsync(int userId, string code, CancellationToken cancellationToken);

		/// <summary>Turns a personal invitation down (it is cancelled). The company code is left alone: it is not the person's to cancel.</summary>
		Task<AcceptOutcome> DenyAsync(int userId, string code, CancellationToken cancellationToken);
	}

	public sealed class JoinAcceptService : IJoinAcceptService
	{
		public JoinAcceptService(IRepository repository, JoinCodes codes, JoinAttemptThrottle throttle, TimeProvider time)
		{
			mRepository = repository;
			mCodes = codes;
			mThrottle = throttle;
			mTime = time;
		}

		public async Task<JoinInfoDto?> GetInvitationInfoAsync(int userId, int invitationId, CancellationToken cancellationToken)
		{
			var invitation = await mRepository.GetQueryable<UserJoinInvitation>(i => i.Id == invitationId && i.IsPersonal == true).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
			if (invitation == null || !IsOpen(invitation)) return null;
			if (!await IsInviteeAsync(userId, invitation, cancellationToken)) return null;
			return await InfoAsync(invitation, cancellationToken);
		}

		public async Task<(AcceptOutcome Outcome, JoinInfoDto? Info)> LookupAsync(int userId, string code, CancellationToken cancellationToken)
		{
			var (outcome, invitation) = await FindAsync(userId, code, cancellationToken);
			if (outcome != AcceptOutcome.Done) return (outcome, null);
			return (AcceptOutcome.Done, await InfoAsync(invitation!, cancellationToken));
		}

		public async Task<(AcceptOutcome Outcome, JoinAcceptedDto? Result)> AcceptAsync(int userId, string code, CancellationToken cancellationToken)
		{
			var (outcome, invitation) = await FindAsync(userId, code, cancellationToken, track: true);
			if (outcome != AcceptOutcome.Done) return (outcome, null);
			invitation = invitation!;

			var membership = await mRepository.GetQueryable<User2Company>(m => m.CompanyId == invitation.CompanyId && m.UserId == userId).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
			if (membership != null && JoinMembership.IsMember(membership.StatusId)) return (AcceptOutcome.AlreadyMember, null);

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
					// Nobody handled it yet; the column cannot be empty, so it names whoever sent the invitation.
					HandledByCompanyUserId = invitation.CreatedByUserId,
					LatestUpdate = Now(),
				});
			}

			// One-time: this code does not work again. (The request keeps pointing at it.)
			invitation.StatusId = UserJoinInvitationStatus.Accepted;
			invitation.UserId ??= userId;
			await mRepository.SaveAsync(cancellationToken);
			mThrottle.Reset(userId);

			var companyName = await mRepository.GetQueryable<Company>(c => c.Id == invitation.CompanyId).AsNoTracking().Select(c => c.Name).SingleAsync(cancellationToken);
			return (AcceptOutcome.Done, new JoinAcceptedDto { Result = needsApproval ? "PendingApproval" : "Joined", CompanyName = companyName });
		}

		public async Task<AcceptOutcome> DenyAsync(int userId, string code, CancellationToken cancellationToken)
		{
			var (outcome, invitation) = await FindAsync(userId, code, cancellationToken, track: true);
			if (outcome != AcceptOutcome.Done) return outcome;

			if (invitation!.IsPersonal == true)
			{
				invitation.StatusId = UserJoinInvitationStatus.Cancelled;
				await mRepository.SaveAsync(cancellationToken);
			}
			mThrottle.Reset(userId);
			return AcceptOutcome.Done;
		}

		// ---- helpers ----

		private DateTime Now() => mTime.GetUtcNow().UtcDateTime;

		private bool IsOpen(UserJoinInvitation i) => i.StatusId == UserJoinInvitationStatus.Active && i.ExpirationDate > Now();

		/// <summary>The open invitation with this code that the user may use. A wrong code counts against the user; a right one for another address does not.</summary>
		private async Task<(AcceptOutcome Outcome, UserJoinInvitation? Invitation)> FindAsync(int userId, string code, CancellationToken cancellationToken, bool track = false)
		{
			if (mThrottle.IsLocked(userId)) return (AcceptOutcome.Locked, null);

			var normalized = JoinCodes.Normalize(code);
			UserJoinInvitation? invitation = null;
			if (normalized != null)
			{
				var hash = mCodes.Hash(normalized);
				var query = mRepository.GetQueryable<UserJoinInvitation>(i => i.JoinCode == hash);
				invitation = await (track ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
			}

			if (invitation == null || !IsOpen(invitation))
			{
				mThrottle.RecordFailure(userId);
				return (AcceptOutcome.InvalidCode, null);
			}
			if (invitation.IsPersonal == true && !await IsInviteeAsync(userId, invitation, cancellationToken)) return (AcceptOutcome.WrongAccount, null);
			return (AcceptOutcome.Done, invitation);
		}

		/// <summary>True for the user the invitation was sent to: the one it was matched to when sent, or whoever has its address.</summary>
		private async Task<bool> IsInviteeAsync(int userId, UserJoinInvitation invitation, CancellationToken cancellationToken)
		{
			if (invitation.UserId == userId) return true;
			var email = await mRepository.GetQueryable<User>(u => u.Id == userId).AsNoTracking().Select(u => u.Email).SingleOrDefaultAsync(cancellationToken);
			return JoinMembership.EmailMatches(email, invitation.Email);
		}

		private async Task<JoinInfoDto> InfoAsync(UserJoinInvitation invitation, CancellationToken cancellationToken)
		{
			var companyName = await mRepository.GetQueryable<Company>(c => c.Id == invitation.CompanyId).AsNoTracking().Select(c => c.Name).SingleAsync(cancellationToken);
			var inviter = await mRepository.GetQueryable<User>(u => u.Id == invitation.CreatedByUserId).AsNoTracking().Select(u => new { u.DisplayName, u.FullName, u.UserName }).SingleOrDefaultAsync(cancellationToken);
			return new JoinInfoDto
			{
				InvitationId = invitation.IsPersonal == true ? invitation.Id : null,
				CompanyName = companyName,
				InviterName = inviter == null ? null : new[] { inviter.DisplayName, inviter.FullName, inviter.UserName }.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
				InviteeName = invitation.Name,
				Message = invitation.Message,
				RequiresApproval = invitation.DoesNotRequireAdminApproval != true,
				ExpirationDate = invitation.ExpirationDate,
			};
		}

		private readonly IRepository mRepository;
		private readonly JoinCodes mCodes;
		private readonly JoinAttemptThrottle mThrottle;
		private readonly TimeProvider mTime;
	}
}
