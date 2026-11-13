using api_server.Controllers.Base;
using api_server.Users;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Companies
{
	/// <summary>
	/// Company-wide reads for the company's admins. Everything is scoped to the caller's current company
	/// (CompanyId claim); callers without a current company get 403.
	/// </summary>
	public class CompanyAdminController : AuthAPIController
	{
		public CompanyAdminController(ICompanyAdminService companyAdmin)
		{
			mCompanyAdmin = companyAdmin;
		}

		[HttpGet("/api/company-admin/company")]
		[ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetCompany(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			var company = await mCompanyAdmin.GetCompanyAsync(companyId, cancellationToken);
			return company == null ? NotFound() : Ok(company);
		}

		/// <summary>Active users of the caller's current company.</summary>
		[HttpGet("/api/company-admin/active-company-users")]
		[ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetActiveCompanyUsers(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return Ok(await mCompanyAdmin.GetActiveCompanyUsersAsync(companyId, cancellationToken));
		}

		[HttpGet("/api/company-admin/not-active-related-users")]
		[ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetNotActiveCompanyRelatedUsers(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return Ok(await mCompanyAdmin.GetNotActiveCompanyRelatedUsersAsync(companyId, cancellationToken));
		}

		[HttpGet("/api/company-admin/user2company")]
		[ProducesResponseType(typeof(IReadOnlyList<User2CompanyDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetUser2Company(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return Ok(await mCompanyAdmin.GetUser2CompanyAsync(companyId, cancellationToken));
		}

		[HttpGet("/api/company-admin/latest-user-sessions")]
		[ProducesResponseType(typeof(IReadOnlyList<LatestUserSessionDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetLatestUserSessions(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return Ok(await mCompanyAdmin.GetLatestUserSessionsAsync(companyId, cancellationToken));
		}

		[HttpGet("/api/company-admin/permission-roles")]
		[ProducesResponseType(typeof(IReadOnlyList<PermissionRoleDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetPermissionRoles(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return Ok(await mCompanyAdmin.GetPermissionRolesAsync(companyId, cancellationToken));
		}

		[HttpGet("/api/company-admin/user-groups")]
		[ProducesResponseType(typeof(IReadOnlyList<UserGroupDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetUserGroups(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return Ok(await mCompanyAdmin.GetUserGroupsAsync(companyId, cancellationToken));
		}

		[HttpGet("/api/company-admin/user2group")]
		[ProducesResponseType(typeof(IReadOnlyList<User2GroupDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetUser2Groups(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return Ok(await mCompanyAdmin.GetUser2GroupsAsync(companyId, cancellationToken));
		}

		[HttpGet("/api/company-admin/user-join-invitations")]
		[ProducesResponseType(typeof(IReadOnlyList<UserJoinInvitationDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetUserJoinInvitations(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return Ok(await mCompanyAdmin.GetUserJoinInvitationsAsync(companyId, cancellationToken));
		}

		[HttpGet("/api/company-admin/user-join-requests")]
		[ProducesResponseType(typeof(IReadOnlyList<UserJoinRequestDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetUserJoinRequests(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return Ok(await mCompanyAdmin.GetUserJoinRequestsAsync(companyId, cancellationToken));
		}

		private ObjectResult NoCompany() => Problem(detail: "The user has no current company.", statusCode: StatusCodes.Status403Forbidden);

		private readonly ICompanyAdminService mCompanyAdmin;
	}
}
