using api_server.Services;
using System.Net.Mail;
using api_server.Controllers.Join.Dto;
using api_server.Services.Notifications;
using db_model.Companies;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using exs.notifications_service.Interfaces;
using exs.notifications_service.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api_server.Controllers.Join.Services
{
	public enum JoinOutcome
	{
		Done,
		NotFound,
		InvalidEmail,
		/// <summary>The person with that address is already a member of the company.</summary>
		AlreadyMember,
		RoleNotAllowed,
		GroupNotAllowed,
		/// <summary>The invitation is not in a state that allows this (an accepted one cannot be renewed or revoked, and so on).</summary>
		WrongState,
		/// <summary>An admin cannot approve their own request.</summary>
		SelfNotAllowed,
		/// <summary>The invitation names no role and the company has no default one to give.</summary>
		NoRole,
		/// <summary>The person is suspended in the company (or removed by the site admin), so a request cannot make them a member.</summary>
		MemberBlocked,
	}

	/// <summary>The company side of joining: invitations, the company join code, and the requests that come out of them.</summary>
	public interface ICompanyJoinService
	{
		Task<IReadOnlyList<JoinInvitationRowDto>> ListInvitationsAsync(int companyId, CancellationToken cancellationToken);

		/// <summary>Cancels the open invitations to the same address, makes a new one (it uses the company's code) and sends the email.</summary>
		Task<(JoinOutcome Outcome, InvitationResultDto? Result)> InviteAsync(int companyId, int userId, InviteUserDto request, string? portalBaseUrl, CancellationToken cancellationToken);

		/// <summary>Sends the email of an open invitation again, with the same code and the same expiry.</summary>
		Task<(JoinOutcome Outcome, InvitationResultDto? Result)> ResendAsync(int companyId, int invitationId, string? portalBaseUrl, CancellationToken cancellationToken);

		/// <summary>Gives an expired (or open) invitation a new expiry, counted from now by the configured length, and sends the email again.</summary>
		Task<(JoinOutcome Outcome, InvitationResultDto? Result)> RenewAsync(int companyId, int invitationId, string? portalBaseUrl, CancellationToken cancellationToken);

		/// <summary>Cancels an invitation to a person that is still open. The company code is not cancelled this way.</summary>
		Task<JoinOutcome> RevokeAsync(int companyId, int invitationId, CancellationToken cancellationToken);

		/// <summary>The company's anonymous join code; made (with the configured role, no group, admin approval on) when there is none yet.</summary>
		Task<CompanyJoinCodeDto> GetCompanyCodeAsync(int companyId, int userId, CancellationToken cancellationToken);

		/// <summary>New code on the same row, and the settings the admin chose. Whatever the old code was used for stops working.</summary>
		Task<(JoinOutcome Outcome, CompanyJoinCodeDto? Result)> RegenerateCompanyCodeAsync(int companyId, int userId, RegenerateCompanyJoinCodeDto request, CancellationToken cancellationToken);

		/// <summary>Switches joining with the company link on or off (the row goes between Active and Disabled).</summary>
		Task<CompanyJoinCodeDto> SetCompanyCodeEnabledAsync(int companyId, int userId, bool enabled, CancellationToken cancellationToken);

		Task<IReadOnlyList<JoinRequestRowDto>> ListRequestsAsync(int companyId, CancellationToken cancellationToken);

		Task<JoinOutcome> ApproveAsync(int companyId, int adminUserId, int requestId, CancellationToken cancellationToken);

		Task<JoinOutcome> DenyAsync(int companyId, int adminUserId, int requestId, CancellationToken cancellationToken);
	}

	public sealed class CompanyJoinService : ICompanyJoinService
	{
		public CompanyJoinService(IRepository repository, INotificationSender sender, JoinCodes codes, IOptions<JoinOptions> options, TimeProvider time, ILogger<CompanyJoinService> logger)
		{
			mRepository = repository;
			mSender = sender;
			mCodes = codes;
			mOptions = options.Value;
			mTime = time;
			mLogger = logger;
		}

		public async Task<IReadOnlyList<JoinInvitationRowDto>> ListInvitationsAsync(int companyId, CancellationToken cancellationToken)
		{
			var items = await mRepository.GetQueryable<UserJoinInvitation>(i => i.CompanyId == companyId && i.TypeId != UserJoinInvitationType.Company)
				.AsNoTracking().OrderByDescending(i => i.CreationDate).ToListAsync(cancellationToken);
			var creatorIds = items.Select(i => i.CreatedByUserId).Distinct().ToList();
			var creators = await mRepository.GetQueryable<User>(u => creatorIds.Contains(u.Id))
				.AsNoTracking().Select(u => new { u.Id, u.DisplayName, u.FirstName, u.LastName, u.UserName }).ToDictionaryAsync(u => u.Id, cancellationToken);
			var now = Now();
			return items.Select(i => ToRow(i, creators.TryGetValue(i.CreatedByUserId, out var c) ? Pick(c.DisplayName, UserNameHelper.Compose(c.FirstName, c.LastName), c.UserName) : null, now)).ToList();
		}

		public async Task<(JoinOutcome Outcome, InvitationResultDto? Result)> InviteAsync(int companyId, int userId, InviteUserDto request, string? portalBaseUrl, CancellationToken cancellationToken)
		{
			var email = (request.Email ?? "").Trim();
			if (!IsEmailAddress(email)) return (JoinOutcome.InvalidEmail, null);

			if (request.PermissionRoleId is int roleId && !await mRepository.GetQueryable<PermissionRole>(r => r.Id == roleId && (r.CompanyId == null || r.CompanyId == companyId)).AnyAsync(cancellationToken))
				return (JoinOutcome.RoleNotAllowed, null);
			if (request.GroupId is int groupId && !await mRepository.GetQueryable<UserGroup>(g => g.Id == groupId && g.CompanyId == companyId).AnyAsync(cancellationToken))
				return (JoinOutcome.GroupNotAllowed, null);

			var (alreadyMember, knownUserId) = await FindUserByEmailAsync(companyId, email, cancellationToken);
			if (alreadyMember) return (JoinOutcome.AlreadyMember, null);

			// The person is let in with the company's code, so the company has to have one.
			var company = await EnsureCompanyRowAsync(companyId, userId, cancellationToken);

			// A newer invitation to the same address replaces the older one.
			await CancelOpenInvitationsAsync(companyId, email, exceptId: null, cancellationToken);

			var now = Now();
			var invitation = new UserJoinInvitation
			{
				CompanyId = companyId,
				CreatedByUserId = userId,
				JoinQrCodeImageName = "",
				JoinCode = null,
				PermissionRoleId = request.PermissionRoleId,
				GroupId = request.GroupId,
				TypeId = UserJoinInvitationType.User,
				DoesNotRequireAdminApproval = !request.RequireAdminApproval,
				UserId = knownUserId,
				Email = email,
				Name = NullIfBlank(request.Name),
				Message = NullIfBlank(request.Message),
				StatusId = UserJoinInvitationStatus.Active,
				CreationDate = now,
				ExpirationDate = now.AddDays(InviteDays()),
				LatestUpdate = now,
			};
			mRepository.Create(invitation);
			await mRepository.SaveAsync(cancellationToken);

			return (JoinOutcome.Done, await ResultAsync(invitation, company.JoinCode!, userId, portalBaseUrl, cancellationToken));
		}

		public async Task<(JoinOutcome Outcome, InvitationResultDto? Result)> ResendAsync(int companyId, int invitationId, string? portalBaseUrl, CancellationToken cancellationToken)
		{
			var invitation = await FindPersonInvitationAsync(companyId, invitationId, cancellationToken);
			if (invitation == null) return (JoinOutcome.NotFound, null);
			if (invitation.StatusId != UserJoinInvitationStatus.Active || invitation.ExpirationDate <= Now()) return (JoinOutcome.WrongState, null);

			var (alreadyMember, _) = await FindUserByEmailAsync(companyId, invitation.Email!, cancellationToken);
			if (alreadyMember) return (JoinOutcome.AlreadyMember, null);

			var code = await CodeOfAsync(invitation, cancellationToken);
			return (JoinOutcome.Done, await ResultAsync(invitation, code, invitation.CreatedByUserId, portalBaseUrl, cancellationToken));
		}

		public async Task<(JoinOutcome Outcome, InvitationResultDto? Result)> RenewAsync(int companyId, int invitationId, string? portalBaseUrl, CancellationToken cancellationToken)
		{
			var invitation = await FindPersonInvitationAsync(companyId, invitationId, cancellationToken);
			if (invitation == null) return (JoinOutcome.NotFound, null);
			// An accepted one is used up; a cancelled one was replaced or withdrawn on purpose.
			if (invitation.StatusId is not (UserJoinInvitationStatus.Active or UserJoinInvitationStatus.Expired)) return (JoinOutcome.WrongState, null);

			var email = invitation.Email!;
			var (alreadyMember, knownUserId) = await FindUserByEmailAsync(companyId, email, cancellationToken);
			if (alreadyMember) return (JoinOutcome.AlreadyMember, null);

			await CancelOpenInvitationsAsync(companyId, email, exceptId: invitation.Id, cancellationToken);

			var now = Now();
			invitation.UserId = knownUserId;
			invitation.StatusId = UserJoinInvitationStatus.Active;
			invitation.ExpirationDate = now.AddDays(InviteDays());
			invitation.LatestUpdate = now;
			var code = await CodeOfAsync(invitation, cancellationToken);
			await mRepository.SaveAsync(cancellationToken);

			return (JoinOutcome.Done, await ResultAsync(invitation, code, invitation.CreatedByUserId, portalBaseUrl, cancellationToken));
		}

		public async Task<JoinOutcome> RevokeAsync(int companyId, int invitationId, CancellationToken cancellationToken)
		{
			var invitation = await FindPersonInvitationAsync(companyId, invitationId, cancellationToken);
			if (invitation == null) return JoinOutcome.NotFound;
			if (invitation.StatusId is UserJoinInvitationStatus.Accepted) return JoinOutcome.WrongState;
			if (invitation.StatusId is UserJoinInvitationStatus.Cancelled) return JoinOutcome.Done;

			invitation.StatusId = UserJoinInvitationStatus.Cancelled;
			invitation.LatestUpdate = Now();
			await mRepository.SaveAsync(cancellationToken);
			return JoinOutcome.Done;
		}

		public async Task<CompanyJoinCodeDto> GetCompanyCodeAsync(int companyId, int userId, CancellationToken cancellationToken)
		{
			var row = await EnsureCompanyRowAsync(companyId, userId, cancellationToken);
			return await CompanyDtoAsync(row, cancellationToken);
		}

		public async Task<(JoinOutcome Outcome, CompanyJoinCodeDto? Result)> RegenerateCompanyCodeAsync(int companyId, int userId, RegenerateCompanyJoinCodeDto request, CancellationToken cancellationToken)
		{
			if (request.PermissionRoleId is int roleId && !await mRepository.GetQueryable<PermissionRole>(r => r.Id == roleId && (r.CompanyId == null || r.CompanyId == companyId)).AnyAsync(cancellationToken))
				return (JoinOutcome.RoleNotAllowed, null);
			if (request.GroupId is int groupId && !await mRepository.GetQueryable<UserGroup>(g => g.Id == groupId && g.CompanyId == companyId).AnyAsync(cancellationToken))
				return (JoinOutcome.GroupNotAllowed, null);

			var row = await EnsureCompanyRowAsync(companyId, userId, cancellationToken);
			row.PermissionRoleId = request.PermissionRoleId;
			row.GroupId = request.GroupId;
			row.DoesNotRequireAdminApproval = !request.RequireAdminApproval;
			row.JoinCode = await NewCodeAsync(cancellationToken);
			row.LatestUpdate = Now();
			await mRepository.SaveAsync(cancellationToken);
			return (JoinOutcome.Done, await CompanyDtoAsync(row, cancellationToken));
		}

		public async Task<CompanyJoinCodeDto> SetCompanyCodeEnabledAsync(int companyId, int userId, bool enabled, CancellationToken cancellationToken)
		{
			var row = await EnsureCompanyRowAsync(companyId, userId, cancellationToken);
			var status = enabled ? UserJoinInvitationStatus.Active : UserJoinInvitationStatus.Disabled;
			if (row.StatusId != status)
			{
				row.StatusId = status;
				row.LatestUpdate = Now();
				await mRepository.SaveAsync(cancellationToken);
			}
			return await CompanyDtoAsync(row, cancellationToken);
		}

		public async Task<IReadOnlyList<JoinRequestRowDto>> ListRequestsAsync(int companyId, CancellationToken cancellationToken)
		{
			var requests = await mRepository.GetQueryable<UserJoinRequest>(r => r.CompanyId == companyId)
				.AsNoTracking().OrderByDescending(r => r.CreationDate).ToListAsync(cancellationToken);
			var userIds = requests.Select(r => r.UserId).Distinct().ToList();
			var invitationIds = requests.Select(r => r.UserJoinInvitationId).Distinct().ToList();
			var users = await mRepository.GetQueryable<User>(u => userIds.Contains(u.Id))
				.AsNoTracking().Select(u => new { u.Id, u.DisplayName, u.FirstName, u.LastName, u.UserName, u.Email }).ToDictionaryAsync(u => u.Id, cancellationToken);
			var invitations = await mRepository.GetQueryable<UserJoinInvitation>(i => invitationIds.Contains(i.Id))
				.AsNoTracking().Select(i => new { i.Id, i.TypeId, i.Message, i.PermissionRoleId, i.GroupId }).ToDictionaryAsync(i => i.Id, cancellationToken);

			return requests.Select(r =>
			{
				users.TryGetValue(r.UserId, out var u);
				invitations.TryGetValue(r.UserJoinInvitationId, out var i);
				return new JoinRequestRowDto
				{
					Id = r.Id,
					UserId = r.UserId,
					UserName = u == null ? null : Pick(u.DisplayName, UserNameHelper.Compose(u.FirstName, u.LastName), u.UserName),
					Email = u?.Email,
					Message = i?.Message,
					InvitationId = r.UserJoinInvitationId,
					ViaCompanyCode = i?.TypeId == UserJoinInvitationType.Company,
					StatusId = r.StatusId,
					CreationDate = r.CreationDate,
					PermissionRoleId = i?.PermissionRoleId,
					GroupId = i?.GroupId,
				};
			}).ToList();
		}

		public async Task<JoinOutcome> ApproveAsync(int companyId, int adminUserId, int requestId, CancellationToken cancellationToken)
		{
			var request = await mRepository.GetQueryable<UserJoinRequest>(r => r.Id == requestId && r.CompanyId == companyId).SingleOrDefaultAsync(cancellationToken);
			if (request == null) return JoinOutcome.NotFound;
			if (request.StatusId != UserJoinRequestStatus.RequestedByUser) return JoinOutcome.WrongState;
			if (request.UserId == adminUserId) return JoinOutcome.SelfNotAllowed;

			var invitation = await mRepository.GetQueryable<UserJoinInvitation>(i => i.Id == request.UserJoinInvitationId).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
			var grant = await JoinMembership.GrantAsync(mRepository, companyId, request.UserId, invitation?.PermissionRoleId, invitation?.GroupId, cancellationToken);
			if (grant == MembershipGrant.NoRole) return JoinOutcome.NoRole;
			if (grant == MembershipGrant.Blocked) return JoinOutcome.MemberBlocked;

			request.StatusId = UserJoinRequestStatus.Accepted;
			request.HandledByCompanyUserId = adminUserId;
			await mRepository.SaveAsync(cancellationToken);
			return JoinOutcome.Done;
		}

		public async Task<JoinOutcome> DenyAsync(int companyId, int adminUserId, int requestId, CancellationToken cancellationToken)
		{
			var request = await mRepository.GetQueryable<UserJoinRequest>(r => r.Id == requestId && r.CompanyId == companyId).SingleOrDefaultAsync(cancellationToken);
			if (request == null) return JoinOutcome.NotFound;
			if (request.StatusId != UserJoinRequestStatus.RequestedByUser) return JoinOutcome.WrongState;

			request.StatusId = UserJoinRequestStatus.Rejected;
			request.HandledByCompanyUserId = adminUserId;
			await mRepository.SaveAsync(cancellationToken);
			return JoinOutcome.Done;
		}

		// ---- helpers ----

		private DateTime Now() => mTime.GetUtcNow().UtcDateTime;

		private int InviteDays() => Math.Max(1, mOptions.DefaultInviteDays);

		/// <summary>An unused code (retrying on the rare clash with one that exists).</summary>
		private async Task<string> NewCodeAsync(CancellationToken cancellationToken)
		{
			for (var attempt = 0; attempt < 10; attempt++)
			{
				var code = mCodes.Generate();
				if (!await mRepository.GetQueryable<UserJoinInvitation>(i => i.JoinCode == code).AnyAsync(cancellationToken)) return code;
			}
			throw new InvalidOperationException("Could not find an unused join code.");
		}

		/// <summary>An invitation to a person (a User or Personal one) of the company, tracked.</summary>
		private Task<UserJoinInvitation?> FindPersonInvitationAsync(int companyId, int invitationId, CancellationToken cancellationToken) =>
			mRepository.GetQueryable<UserJoinInvitation>(i => i.Id == invitationId && i.CompanyId == companyId && i.TypeId != UserJoinInvitationType.Company).SingleOrDefaultAsync(cancellationToken);

		/// <summary>The code an invitation is accepted with: its own (Personal) or the company's (User).</summary>
		private async Task<string> CodeOfAsync(UserJoinInvitation invitation, CancellationToken cancellationToken)
		{
			if (invitation.JoinCode != null) return invitation.JoinCode;
			return (await EnsureCompanyRowAsync(invitation.CompanyId, invitation.CreatedByUserId, cancellationToken)).JoinCode!;
		}

		/// <summary>The company's anonymous row (Active or Disabled), made when there is none. A second one made by a parallel request is cancelled.</summary>
		private async Task<UserJoinInvitation> EnsureCompanyRowAsync(int companyId, int userId, CancellationToken cancellationToken)
		{
			var rows = await mRepository.GetQueryable<UserJoinInvitation>(i => i.CompanyId == companyId && i.TypeId == UserJoinInvitationType.Company && i.Email == null
					&& (i.StatusId == UserJoinInvitationStatus.Active || i.StatusId == UserJoinInvitationStatus.Disabled))
				.OrderBy(i => i.Id).ToListAsync(cancellationToken);
			if (rows.Count > 0)
			{
				if (rows.Count > 1)
				{
					foreach (var extra in rows.Skip(1)) extra.StatusId = UserJoinInvitationStatus.Cancelled;
					await mRepository.SaveAsync(cancellationToken);
				}
				return rows[0];
			}

			var now = Now();
			var row = new UserJoinInvitation
			{
				CompanyId = companyId,
				CreatedByUserId = userId,
				JoinQrCodeImageName = "",
				TypeId = UserJoinInvitationType.Company,
				PermissionRoleId = await ConfiguredRoleAsync(companyId, cancellationToken),
				GroupId = null,
				DoesNotRequireAdminApproval = false,
				StatusId = UserJoinInvitationStatus.Active,
				CreationDate = now,
				// It does not expire: it lasts until an admin regenerates or switches it off.
				ExpirationDate = NoExpiry,
				LatestUpdate = now,
				JoinCode = await NewCodeAsync(cancellationToken),
			};
			mRepository.Create(row);
			await mRepository.SaveAsync(cancellationToken);
			return row;
		}

		/// <summary>The expiry stored on the company row, which never expires.</summary>
		public static readonly DateTime NoExpiry = new(2999, 12, 31, 0, 0, 0, DateTimeKind.Utc);

		/// <summary>The role named in Join:CompanyCodeRole (the company's own before a system one), else the company's default role.</summary>
		private async Task<int?> ConfiguredRoleAsync(int companyId, CancellationToken cancellationToken)
		{
			var name = mOptions.CompanyCodeRole;
			if (!string.IsNullOrWhiteSpace(name))
			{
				var roles = await mRepository.GetQueryable<PermissionRole>(r => r.CompanyId == null || r.CompanyId == companyId).AsNoTracking()
					.Select(r => new { r.Id, r.Name, r.CompanyId }).ToListAsync(cancellationToken);
				var found = roles.Where(r => string.Equals(r.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)).OrderByDescending(r => r.CompanyId != null).Select(r => (int?)r.Id).FirstOrDefault();
				if (found != null) return found;
			}
			return await JoinMembership.ResolveRoleAsync(mRepository, companyId, null, cancellationToken);
		}

		private async Task<CompanyJoinCodeDto> CompanyDtoAsync(UserJoinInvitation row, CancellationToken cancellationToken)
		{
			var now = Now();
			var open = await mRepository.GetQueryable<UserJoinInvitation>(i => i.CompanyId == row.CompanyId && i.TypeId == UserJoinInvitationType.User
				&& i.StatusId == UserJoinInvitationStatus.Active && i.ExpirationDate > now).CountAsync(cancellationToken);
			return new CompanyJoinCodeDto
			{
				Id = row.Id,
				JoinCode = row.JoinCode!,
				Enabled = row.StatusId == UserJoinInvitationStatus.Active,
				PermissionRoleId = row.PermissionRoleId,
				GroupId = row.GroupId,
				RequireAdminApproval = row.DoesNotRequireAdminApproval != true,
				OpenInvitations = open,
			};
		}

		private async Task CancelOpenInvitationsAsync(int companyId, string email, int? exceptId, CancellationToken cancellationToken)
		{
			var lower = email.ToLower();
			var open = await mRepository.GetQueryable<UserJoinInvitation>(i => i.CompanyId == companyId && i.TypeId != UserJoinInvitationType.Company && i.StatusId == UserJoinInvitationStatus.Active
					&& i.Email != null && i.Email.ToLower() == lower && i.Id != exceptId)
				.ToListAsync(cancellationToken);
			foreach (var old in open) old.StatusId = UserJoinInvitationStatus.Cancelled;
		}

		/// <summary>Whether a member of the company has this address, and the one user who has it (null when nobody, or more than one).</summary>
		private async Task<(bool AlreadyMember, int? UserId)> FindUserByEmailAsync(int companyId, string email, CancellationToken cancellationToken)
		{
			var lower = email.ToLower();
			var ids = await mRepository.GetQueryable<User>(u => u.KindId != UserKind.Contact && u.Email != null && u.Email.ToLower() == lower).AsNoTracking().Select(u => u.Id).ToListAsync(cancellationToken);
			if (ids.Count == 0) return (false, null);
			var statuses = await mRepository.GetQueryable<User2Company>(m => m.CompanyId == companyId && ids.Contains(m.UserId)).AsNoTracking().Select(m => m.StatusId).ToListAsync(cancellationToken);
			return (statuses.Any(JoinMembership.IsMember), ids.Count == 1 ? ids[0] : null);
		}

		private async Task<InvitationResultDto> ResultAsync(UserJoinInvitation invitation, string code, int inviterUserId, string? portalBaseUrl, CancellationToken cancellationToken)
		{
			var inviter = await mRepository.GetQueryable<User>(u => u.Id == inviterUserId).AsNoTracking().Select(u => new { u.DisplayName, u.FirstName, u.LastName, u.UserName }).SingleOrDefaultAsync(cancellationToken);
			var inviterName = inviter == null ? null : Pick(inviter.DisplayName, UserNameHelper.Compose(inviter.FirstName, inviter.LastName), inviter.UserName);
			var emailState = await SendEmailAsync(invitation, code, inviterName ?? "A colleague", portalBaseUrl, cancellationToken);
			return new InvitationResultDto { Invitation = ToRow(invitation, inviterName, Now()), JoinCode = code, EmailState = emailState };
		}

		private async Task<string> SendEmailAsync(UserJoinInvitation invitation, string code, string inviterName, string? portalBaseUrl, CancellationToken cancellationToken)
		{
			var baseUrl = (mOptions.PortalBaseUrl ?? portalBaseUrl)?.TrimEnd('/');
			if (string.IsNullOrWhiteSpace(baseUrl)) return "Failed";
			var companyName = await mRepository.GetQueryable<Company>(c => c.Id == invitation.CompanyId).AsNoTracking().Select(c => c.Name).SingleAsync(cancellationToken);

			var data = new JoinInvitationData
			{
				InviteeName = invitation.Name ?? invitation.Email!.Split('@')[0],
				CompanyName = companyName,
				InviterName = inviterName,
				JoinCode = code,
				AcceptLink = $"{baseUrl}/join?code={Uri.EscapeDataString(code)}",
				Message = invitation.Message,
				ExpiresAtUtc = DateTime.SpecifyKind(invitation.ExpirationDate, DateTimeKind.Utc),
			};
			var envelope = new EmailEnvelope { To = invitation.Email!, ToName = invitation.Name, Ref = $"join-invitation-{invitation.Id}" };

			try
			{
				await mSender.CreateEmailFromTemplateAsync(mRepository, envelope, data, null, cancellationToken);
				await mRepository.SaveAsync(cancellationToken);
				return "Sent";
			}
			catch (TemplateNotFoundException)
			{
				return "NoTemplate";
			}
			catch (InvalidOperationException)
			{
				// The email transport is off, or the templates are not registered.
				return "NotAvailable";
			}
			catch (Exception ex) when (ex is ArgumentException or TemplateRenderException or TemplateModelException)
			{
				// Neither the address nor the code goes to the log.
				mLogger.LogWarning(ex, "The join invitation {InvitationId} could not be turned into an email", invitation.Id);
				return "Failed";
			}
		}

		private static JoinInvitationRowDto ToRow(UserJoinInvitation i, string? createdByName, DateTime now) => new()
		{
			Id = i.Id,
			TypeId = i.TypeId,
			Email = i.Email,
			Name = i.Name,
			Message = i.Message,
			PermissionRoleId = i.PermissionRoleId,
			GroupId = i.GroupId,
			RequireAdminApproval = i.DoesNotRequireAdminApproval != true,
			StatusId = i.StatusId,
			IsExpired = i.StatusId == UserJoinInvitationStatus.Expired || (i.StatusId == UserJoinInvitationStatus.Active && i.ExpirationDate <= now),
			CreationDate = i.CreationDate,
			ExpirationDate = i.ExpirationDate,
			CreatedByUserId = i.CreatedByUserId,
			CreatedByName = createdByName,
		};

		private static bool IsEmailAddress(string value) =>
			value.Length <= 320 && MailAddress.TryCreate(value, out var parsed) && parsed.Address == value && value.Contains('@');

		private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

		private static string Pick(params string?[] names) => names.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "";

		private readonly IRepository mRepository;
		private readonly INotificationSender mSender;
		private readonly JoinCodes mCodes;
		private readonly JoinOptions mOptions;
		private readonly TimeProvider mTime;
		private readonly ILogger<CompanyJoinService> mLogger;
	}
}
