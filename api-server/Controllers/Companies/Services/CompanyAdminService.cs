using api_server.Users;
using db_model.Companies;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using exs.modelCommons.UserManagement;
using Microsoft.EntityFrameworkCore;

namespace api_server.Companies
{
	/// <summary>
	/// Read access to everything that belongs to one company, for that company's admins.
	/// Every method is scoped to the <c>companyId</c> passed in (the caller's current company).
	/// </summary>
	public interface ICompanyAdminService
	{
		/// <summary>Null when the company does not exist.</summary>
		Task<CompanyDto?> GetCompanyAsync(int companyId, CancellationToken cancellationToken);

		/// <summary>Active users whose CompanyId is the company (delegates to <see cref="IUserService"/>).</summary>
		Task<IReadOnlyList<UserDto>> GetActiveCompanyUsersAsync(int companyId, CancellationToken cancellationToken);

		/// <summary>
		/// Users related to the company without being active members: those with a non-active User2Company
		/// status plus those with a user join request to the company (any request status).
		/// </summary>
		Task<IReadOnlyList<UserDto>> GetNotActiveCompanyRelatedUsersAsync(int companyId, CancellationToken cancellationToken);

		Task<IReadOnlyList<User2CompanyDto>> GetUser2CompanyAsync(int companyId, CancellationToken cancellationToken);

		/// <summary>
		/// One session per user of the company: the latest (by LatestUpdate) active session or, for a user
		/// without an active session, the latest session of any status. Users without sessions are omitted.
		/// </summary>
		Task<IReadOnlyList<LatestUserSessionDto>> GetLatestUserSessionsAsync(int companyId, CancellationToken cancellationToken);

		/// <summary>System-wide roles (CompanyId null) plus the company's own.</summary>
		Task<IReadOnlyList<PermissionRoleDto>> GetPermissionRolesAsync(int companyId, CancellationToken cancellationToken);

		Task<IReadOnlyList<UserGroupDto>> GetUserGroupsAsync(int companyId, CancellationToken cancellationToken);

		Task<IReadOnlyList<User2GroupDto>> GetUser2GroupsAsync(int companyId, CancellationToken cancellationToken);

		Task<IReadOnlyList<UserJoinInvitationDto>> GetUserJoinInvitationsAsync(int companyId, CancellationToken cancellationToken);

		Task<IReadOnlyList<UserJoinRequestDto>> GetUserJoinRequestsAsync(int companyId, CancellationToken cancellationToken);
	}

	public sealed class CompanyAdminService : ICompanyAdminService
	{
		private readonly IRepository mRepository;
		private readonly IUserService mUsers;

		public CompanyAdminService(IRepository repository, IUserService users)
		{
			mRepository = repository;
			mUsers = users;
		}

		public Task<IReadOnlyList<UserDto>> GetActiveCompanyUsersAsync(int companyId, CancellationToken cancellationToken) =>
			mUsers.GetActiveCompanyUsersAsync(companyId, cancellationToken);

		public async Task<IReadOnlyList<UserDto>> GetNotActiveCompanyRelatedUsersAsync(int companyId, CancellationToken cancellationToken)
		{
			var notActiveMemberIds = await mRepository
				.GetQueryable<User2Company>(x => x.CompanyId == companyId && x.StatusId != User2CompanyStatus.Active)
				.Select(x => x.UserId)
				.ToListAsync(cancellationToken);

			var requesterIds = await mRepository
				.GetQueryable<UserJoinRequest>(x => x.CompanyId == companyId)
				.Select(x => x.UserId)
				.ToListAsync(cancellationToken);

			var userIds = notActiveMemberIds.Union(requesterIds).ToList();
			return await mUsers.GetUsersByIdsAsync(userIds, cancellationToken);
		}

		public async Task<CompanyDto?> GetCompanyAsync(int companyId, CancellationToken cancellationToken)
		{
			var company = await mRepository.GetQueryable<Company>(c => c.Id == companyId)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			return company == null ? null : new CompanyDto(company);
		}

		public async Task<IReadOnlyList<User2CompanyDto>> GetUser2CompanyAsync(int companyId, CancellationToken cancellationToken)
		{
			var items = await mRepository.GetQueryable<User2Company>(x => x.CompanyId == companyId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return items.Select(x => new User2CompanyDto(x)).ToList();
		}

		public async Task<IReadOnlyList<LatestUserSessionDto>> GetLatestUserSessionsAsync(int companyId, CancellationToken cancellationToken)
		{
			var companyUserIds = mRepository.GetQueryable<User>(u => u.CompanyId == companyId).Select(u => u.Id);

			var activeSessions = await LatestSessionPerUser(
					mRepository.GetQueryable<UserSession>(s => companyUserIds.Contains(s.UserId) && s.StatusId == UserSessionStatus.Active))
				.ToListAsync(cancellationToken);

			// Only users without an active session fall back to their latest session of any status.
			var usersWithActive = activeSessions.Select(s => s.UserId).ToList();
			var fallbackSessions = await LatestSessionPerUser(
					mRepository.GetQueryable<UserSession>(s => companyUserIds.Contains(s.UserId) && !usersWithActive.Contains(s.UserId)))
				.ToListAsync(cancellationToken);

			return activeSessions.Concat(fallbackSessions)
				.OrderBy(s => s.UserId)
				.Select(s => new LatestUserSessionDto(s))
				.ToList();
		}

		public async Task<IReadOnlyList<PermissionRoleDto>> GetPermissionRolesAsync(int companyId, CancellationToken cancellationToken)
		{
			var items = await mRepository.GetQueryable<PermissionRole>(r => r.CompanyId == null || r.CompanyId == companyId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return items.Select(r => new PermissionRoleDto(r)).ToList();
		}

		// "as roles" (see GetPermissionRolesAsync), but UserGroup.CompanyId is required, so there are no system-wide groups.
		public async Task<IReadOnlyList<UserGroupDto>> GetUserGroupsAsync(int companyId, CancellationToken cancellationToken)
		{
			var items = await mRepository.GetQueryable<UserGroup>(g => g.CompanyId == companyId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return items.Select(g => new UserGroupDto(g)).ToList();
		}

		public async Task<IReadOnlyList<User2GroupDto>> GetUser2GroupsAsync(int companyId, CancellationToken cancellationToken)
		{
			var items = await mRepository.GetQueryable<User2Group>(x => x.CompanyId == companyId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return items.Select(x => new User2GroupDto(x)).ToList();
		}

		public async Task<IReadOnlyList<UserJoinInvitationDto>> GetUserJoinInvitationsAsync(int companyId, CancellationToken cancellationToken)
		{
			var items = await mRepository.GetQueryable<UserJoinInvitation>(x => x.CompanyId == companyId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return items.Select(x => new UserJoinInvitationDto(x)).ToList();
		}

		public async Task<IReadOnlyList<UserJoinRequestDto>> GetUserJoinRequestsAsync(int companyId, CancellationToken cancellationToken)
		{
			var items = await mRepository.GetQueryable<UserJoinRequest>(x => x.CompanyId == companyId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return items.Select(x => new UserJoinRequestDto(x)).ToList();
		}

		private static IQueryable<UserSession> LatestSessionPerUser(IQueryable<UserSession> sessions) =>
			sessions.AsNoTracking()
				.GroupBy(s => s.UserId)
				.Select(g => g.OrderByDescending(s => s.LatestUpdate).First());
	}
}
