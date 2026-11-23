using api_server.Controllers.Base;
using api_server.Controllers.Roles.Dto;
using api_server.Controllers.Roles.Services;
using api_server.core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Roles
{
	/// <summary>
	/// The Roles screen, for Portal Admin only: the roles of the caller's company (the system-wide ones and its own), which menu items
	/// each may use, and which users have which role. Only view is enforced (it decides what GET /api/menu returns); create, edit and
	/// delete are stored and nothing else yet. New roles belong to the caller's company; the system-wide ones can be given access
	/// rights and members but not renamed or deleted.
	/// </summary>
	[Authorize(Policy = AuthPolicies.PortalAdmin)]
	public class RolesAdminController : AuthAPIController
	{
		public RolesAdminController(IRoleMenuPermissionsService permissions, IRoleAdminService roles)
		{
			mPermissions = permissions;
			mRoles = roles;
		}

		// ---- the roles ----

		/// <summary>The roles, with how many users hold each in the caller's company and how much of the menu each may use.</summary>
		[HttpGet("/api/admin/roles")]
		[ProducesResponseType(typeof(IReadOnlyList<RoleSummaryDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Ok(await mPermissions.ListRolesAsync(companyId, cancellationToken));
		}

		/// <summary>
		/// Creates a role of the caller's company, optionally starting with the access rights of another role (which is also how a role
		/// is duplicated). 201 with the new role; 400 for a bad name or an unknown role to copy; 409 when the name is taken.
		/// </summary>
		[HttpPost("/api/admin/roles")]
		[ProducesResponseType(typeof(RoleSummaryDto), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> CreateRole([FromBody] CreateRoleDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var (outcome, roleId) = await mRoles.CreateAsync(companyId, request, cancellationToken);
			if (outcome != RoleAdminOutcome.Done) return Failure(outcome);

			var created = (await mPermissions.ListRolesAsync(companyId, cancellationToken)).Single(r => r.Id == roleId);
			return Created($"/api/admin/roles/{roleId}/menu-permissions", created);
		}

		/// <summary>
		/// Renames a role of the caller's company and/or changes its description (a field that is left out stays; an empty description
		/// clears it). 404 for an unknown role, 409 for a system-wide role or a name that is taken.
		/// </summary>
		[HttpPatch("/api/admin/roles/{roleId:int}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> UpdateRole(int roleId, [FromBody] UpdateRoleDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var outcome = await mRoles.UpdateAsync(companyId, roleId, request, cancellationToken);
			return outcome == RoleAdminOutcome.Done ? NoContent() : Failure(outcome, roleId);
		}

		/// <summary>
		/// Deletes a role of the caller's company, with its access rights. 404 for an unknown role; 409 for a system-wide role or one
		/// that users still have (give them another role first).
		/// </summary>
		[HttpDelete("/api/admin/roles/{roleId:int}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> DeleteRole(int roleId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var outcome = await mRoles.DeleteAsync(companyId, roleId, cancellationToken);
			return outcome == RoleAdminOutcome.Done ? NoContent() : Failure(outcome, roleId);
		}

		// ---- access rights ----

		/// <summary>The whole menu as a tree, every item with the role's flags (false where the role has no row).</summary>
		[HttpGet("/api/admin/roles/{roleId:int}/menu-permissions")]
		[ProducesResponseType(typeof(RoleMenuPermissionsDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetMenuPermissions(int roleId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var result = await mPermissions.GetAsync(companyId, roleId, cancellationToken);
			return result == null ? NoSuchRole(roleId) : Ok(result);
		}

		/// <summary>
		/// Replaces the role's flags on the menu. List every item the role may do anything with; an item left out is not allowed.
		/// Ticking create, edit or delete implies view. 404 for an unknown role, 400 for an unknown or repeated item, 409 for
		/// Portal Admin (it always has everything).
		/// </summary>
		[HttpPut("/api/admin/roles/{roleId:int}/menu-permissions")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> SetMenuPermissions(int roleId, [FromBody] SetRoleMenuPermissionsDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return await mPermissions.SetAsync(companyId, roleId, request, cancellationToken) switch
			{
				SetRoleMenuPermissionsOutcome.Updated => NoContent(),
				SetRoleMenuPermissionsOutcome.RoleNotFound => NoSuchRole(roleId),
				SetRoleMenuPermissionsOutcome.ReadOnlyRole => Problem(title: "Role is read-only", detail: $"The role {AuthPolicies.PortalAdminRole} always has full access and cannot be changed here.", statusCode: StatusCodes.Status409Conflict),
				SetRoleMenuPermissionsOutcome.UnknownMenuItem => Problem(title: "Unknown menu item", detail: "An entry names a menu item that is not in the menu.", statusCode: StatusCodes.Status400BadRequest),
				SetRoleMenuPermissionsOutcome.DuplicateMenuItem => Problem(title: "Repeated menu item", detail: "Two entries name the same menu item.", statusCode: StatusCodes.Status400BadRequest),
				_ => throw new InvalidOperationException("Unhandled outcome"),
			};
		}

		// ---- users ----

		/// <summary>The users of the caller's company (deleted ones left out), each with the role they have.</summary>
		[HttpGet("/api/admin/roles/users")]
		[ProducesResponseType(typeof(IReadOnlyList<RoleUserDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Ok(await mRoles.ListUsersAsync(companyId, UserId, cancellationToken));
		}

		/// <summary>
		/// Gives the role to these users of the caller's company, replacing the role each had. 404 for an unknown role; 400 when a user is
		/// not a member; 409 when the caller is in the list or a user was suspended by the site admin.
		/// </summary>
		[HttpPost("/api/admin/roles/{roleId:int}/members")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> AssignUsers(int roleId, [FromBody] AssignRoleUsersDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var outcome = await mRoles.AssignUsersAsync(companyId, UserId, roleId, request.UserIds ?? [], cancellationToken);
			return outcome == RoleAdminOutcome.Done ? NoContent() : Failure(outcome, roleId);
		}

		private ObjectResult NoSuchRole(int? roleId) =>
			Problem(title: "Not Found", detail: roleId is int id ? $"There is no role with id {id} that this company can use." : "There is no such role that this company can use.", statusCode: StatusCodes.Status404NotFound);

		private ObjectResult Failure(RoleAdminOutcome outcome, int? roleId = null) => outcome switch
		{
			RoleAdminOutcome.RoleNotFound => NoSuchRole(roleId),
			RoleAdminOutcome.SourceRoleNotFound => Problem(title: "Unknown role to copy", detail: "The role to copy the access rights from is not one this company can use.", statusCode: StatusCodes.Status400BadRequest),
			RoleAdminOutcome.InvalidName => Problem(title: "Invalid name", detail: $"A role name has 1 to {RoleAdminRules.MaxNameLength} characters.", statusCode: StatusCodes.Status400BadRequest),
			RoleAdminOutcome.NameTaken => Problem(title: "Name taken", detail: "Another role this company can use already has that name.", statusCode: StatusCodes.Status409Conflict),
			RoleAdminOutcome.SystemRole => Problem(title: "System role", detail: "A role every company uses cannot be renamed or deleted; give it access rights and members instead.", statusCode: StatusCodes.Status409Conflict),
			RoleAdminOutcome.HasMembers => Problem(title: "Role in use", detail: "Users still have this role. Give them another role first.", statusCode: StatusCodes.Status409Conflict),
			RoleAdminOutcome.CannotChangeSelf => Problem(title: "Cannot change your own role", detail: "Nobody can change their own role here, so that nobody can lock themselves out.", statusCode: StatusCodes.Status409Conflict),
			RoleAdminOutcome.UserNotFound => Problem(title: "Unknown user", detail: "A user in the list is not a member of this company.", statusCode: StatusCodes.Status400BadRequest),
			RoleAdminOutcome.MemberNotEditable => Problem(title: "User cannot be changed", detail: "A user in the list was suspended by the site admin; only the site admin can change them.", statusCode: StatusCodes.Status409Conflict),
			_ => throw new InvalidOperationException("Unhandled outcome"),
		};

		private readonly IRoleMenuPermissionsService mPermissions;
		private readonly IRoleAdminService mRoles;
	}
}
