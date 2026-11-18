using api_server.Controllers.Base;
using api_server.Controllers.Companies.Dto;
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
		[HttpGet("/api/company-admin/user-management-context")]
		[ProducesResponseType(typeof(CompanyUserManagementContextDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetUserManagementContext(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return Ok(await mCompanyAdmin.GetUserManagementContextAsync(companyId, cancellationToken));
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

		/// <summary>
		/// Partial update of a user's membership in the caller's current company: the status (suspend, reactivate or
		/// delete) and/or the permission role (see <see cref="ICompanyAdminService.PatchUser2CompanyAsync"/>). 204 on success.
		/// </summary>
		[HttpPatch("/api/company-admin/user2company/{userId}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status409Conflict)]
		public async Task<IActionResult> PatchUser2Company(int userId, [FromBody] User2CompanyPatchDto body, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return PatchResult(await mCompanyAdmin.PatchUser2CompanyAsync(companyId, UserId, userId, body, cancellationToken));
		}

		/// <summary>
		/// Adds a member of the caller's current company to groups and/or removes them from groups
		/// (see <see cref="ICompanyAdminService.PatchUser2GroupsAsync"/>). 204 on success.
		/// </summary>
		[HttpPatch("/api/company-admin/user2group/{userId}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> PatchUser2Group(int userId, [FromBody] User2GroupPatchDto body, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return PatchResult(await mCompanyAdmin.PatchUser2GroupsAsync(companyId, userId, body, cancellationToken));
		}

		//[HttpGet("/api/company-admin/latest-user-sessions")]
		//[ProducesResponseType(typeof(IReadOnlyList<LatestUserSessionDto>), StatusCodes.Status200OK)]
		//[ProducesResponseType(StatusCodes.Status403Forbidden)]
		//public async Task<IActionResult> GetLatestUserSessions(CancellationToken cancellationToken)
		//{
		//	if (CompanyId is not int companyId) return NoCompany();
		//	return Ok(await mCompanyAdmin.GetLatestUserSessionsAsync(companyId, cancellationToken));
		//}

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

		/// <summary>
		/// Creates a group in the caller's current company, optionally with its first members
		/// (see <see cref="ICompanyAdminService.CreateUserGroupAsync"/>). 201 with the new group.
		/// </summary>
		[HttpPost("/api/company-admin/user-groups")]
		[ProducesResponseType(typeof(UserGroupDto), StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status409Conflict)]
		public async Task<IActionResult> CreateUserGroup([FromBody] UserGroupCreateDto body, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			var (outcome, group) = await mCompanyAdmin.CreateUserGroupAsync(companyId, body, cancellationToken);
			return outcome == CompanyAdminPatchOutcome.Updated ? Created($"/api/company-admin/user-groups/{group!.Id}", group) : PatchResult(outcome);
		}

		/// <summary>
		/// Partial update of a group of the caller's current company: name, description, colour and/or members
		/// (see <see cref="ICompanyAdminService.PatchUserGroupAsync"/>). 204 on success.
		/// </summary>
		[HttpPatch("/api/company-admin/user-groups/{groupId}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status409Conflict)]
		public async Task<IActionResult> PatchUserGroup(int groupId, [FromBody] UserGroupPatchDto body, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return PatchResult(await mCompanyAdmin.PatchUserGroupAsync(companyId, groupId, body, cancellationToken));
		}

		/// <summary>Deletes a group of the caller's current company together with its memberships. 204 on success.</summary>
		[HttpDelete("/api/company-admin/user-groups/{groupId}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> DeleteUserGroup(int groupId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return NoCompany();
			return PatchResult(await mCompanyAdmin.DeleteUserGroupAsync(companyId, groupId, cancellationToken));
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

		private IActionResult PatchResult(CompanyAdminPatchOutcome outcome) => outcome switch
		{
			CompanyAdminPatchOutcome.Updated => NoContent(),
			CompanyAdminPatchOutcome.NotFound => NotFound(),
			CompanyAdminPatchOutcome.SelfNotAllowed => Problem(detail: "You can't change your own membership.", statusCode: StatusCodes.Status403Forbidden),
			CompanyAdminPatchOutcome.StatusNotAllowed => Problem(detail: "StatusId must be Active, Suspended or Deleted.", statusCode: StatusCodes.Status400BadRequest),
			CompanyAdminPatchOutcome.RoleNotAllowed => Problem(detail: "PermissionRoleId is not a role this company can use.", statusCode: StatusCodes.Status400BadRequest),
			CompanyAdminPatchOutcome.UserNotAllowed => Problem(detail: "AddUserIds contains a user who is not a member of this company.", statusCode: StatusCodes.Status400BadRequest),
			CompanyAdminPatchOutcome.NameTaken => Problem(detail: "Another group already has this name.", statusCode: StatusCodes.Status409Conflict),
			CompanyAdminPatchOutcome.GroupNotAllowed => Problem(detail: "AddGroupIds contains a group that is not in this company.", statusCode: StatusCodes.Status400BadRequest),
			_ => Problem(detail: "The user's current status doesn't allow this change.", statusCode: StatusCodes.Status409Conflict),
		};

		private ObjectResult NoCompany() => Problem(detail: "The user has no current company.", statusCode: StatusCodes.Status403Forbidden);

		private readonly ICompanyAdminService mCompanyAdmin;
	}
}
