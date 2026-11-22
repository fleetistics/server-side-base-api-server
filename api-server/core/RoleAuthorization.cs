using System.IdentityModel.Tokens.Jwt;
using api_server.Auth;
using db_model.Companies;
using exs.Database.Commons.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace api_server.core
{
	/// <summary>The caller's role in their current company must be the system-wide role (permission_role.CompanyId is null) with this name.</summary>
	public sealed class RoleRequirement : IAuthorizationRequirement
	{
		public RoleRequirement(string roleName)
		{
			RoleName = roleName;
		}

		public string RoleName { get; }
	}

	/// <summary>Whether a user has a given system-wide role in a company.</summary>
	public interface IRoleLookup
	{
		Task<bool> HasRoleAsync(int userId, int companyId, string roleName, CancellationToken cancellationToken);
	}

	public sealed class RoleLookup : IRoleLookup
	{
		public RoleLookup(IRepository repository)
		{
			mRepository = repository;
		}

		public Task<bool> HasRoleAsync(int userId, int companyId, string roleName, CancellationToken cancellationToken) =>
			mRepository.GetQueryable<User2Company>(m => m.CompanyId == companyId && m.UserId == userId)
				.Join(
					mRepository.GetQueryable<PermissionRole>(r => r.CompanyId == null && r.Name == roleName),
					m => m.PermissionRoleId, r => r.Id, (m, r) => r.Id)
				.AnyAsync(cancellationToken);

		private readonly IRepository mRepository;
	}

	/// <summary>
	/// Decides <see cref="RoleRequirement"/> from the database on every call, not from the access token: the token carries no
	/// role, and reading it live means a role taken away stops working at once instead of when the token is next renewed
	/// (the same rule GET /api/menu follows). The company is the token's CompanyId claim; a caller without one has no role.
	/// Not succeeding is a refusal (403 for a signed-in caller).
	/// </summary>
	public sealed class RoleAuthorizationHandler : AuthorizationHandler<RoleRequirement>
	{
		public RoleAuthorizationHandler(IRoleLookup roles)
		{
			mRoles = roles;
		}

		protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleRequirement requirement)
		{
			if (!int.TryParse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)) return;
			if (!int.TryParse(context.User.FindFirst(TokenService.CompanyIdClaim)?.Value, out var companyId)) return;

			var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
			if (await mRoles.HasRoleAsync(userId, companyId, requirement.RoleName, cancellationToken))
			{
				context.Succeed(requirement);
			}
		}

		private readonly IRoleLookup mRoles;
	}
}
