using api_server.Controllers.Base;
using api_server.Controllers.Roles.Dto;
using api_server.Controllers.Roles.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Roles
{
	/// <summary>
	/// The Admin ▸ User Management ▸ Roles tab: the roles of the caller's company (the system roles and its own), which menu items
	/// each may use there, and which users have which role. A company changes a system role only for itself: saving its access rights
	/// stores an override that replaces the defaults of that role as a whole, and DELETE .../menu-permissions goes back to the defaults.
	/// New roles belong to the caller's company; the system roles can be given access rights and members but not renamed or deleted.
	/// The company comes from the access token, so one company cannot touch another's roles or users.
	///
	/// NOTE: access is not restricted by role yet. Any signed-in member of the company can call these endpoints; the menu only hides
	/// the tab. Deciding who may call this is still open.
	///
	/// Only view is enforced (it decides what GET /api/menu returns); create, edit and delete are stored and nothing else yet.
	/// </summary>
	public class CompanyRolesController : AuthAPIController
	{
		public CompanyRolesController(IRoleMenuPermissionsService permissions, IRoleAdminService roles)
		{
			mPermissions = permissions;
			mRoles = roles;
		}

		// ---- the roles ----

		/// <summary>The roles, with how many users hold each in the caller's company and how much of the menu each may use there.</summary>
		[HttpGet("/api/company/roles")]
		[ProducesResponseType(typeof(IReadOnlyList<RoleSummaryDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Ok(await mPermissions.ListRolesAsync(companyId, cancellationToken));
		}

		/// <summary>
		/// Creates a role of the caller's company, optionally starting with the access rights a role gives the company today (which is
		/// also how a role is duplicated). 201 with the new role; 400 for a bad name or an unknown role to copy; 409 when the name is taken.
		/// </summary>
		[HttpPost("/api/company/roles")]
		[ProducesResponseType(typeof(RoleSummaryDto), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> CreateRole([FromBody] CreateRoleDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var (outcome, roleId) = await mRoles.CreateAsync(companyId, request, cancellationToken);
			if (outcome != RoleAdminOutcome.Done) return RolesProblems.Failure(this, outcome);

			var created = (await mPermissions.ListRolesAsync(companyId, cancellationToken)).Single(r => r.Id == roleId);
			return Created($"/api/company/roles/{roleId}/menu-permissions", created);
		}

		/// <summary>
		/// Renames a role of the caller's company and/or changes its description (a field that is left out stays; an empty description
		/// clears it). 404 for an unknown role, 409 for a system role or a name that is taken.
		/// </summary>
		[HttpPatch("/api/company/roles/{roleId:int}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> UpdateRole(int roleId, [FromBody] UpdateRoleDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var outcome = await mRoles.UpdateAsync(companyId, roleId, request, cancellationToken);
			return outcome == RoleAdminOutcome.Done ? NoContent() : RolesProblems.Failure(this, outcome, roleId);
		}

		/// <summary>
		/// Deletes a role of the caller's company, with its access rights. 404 for an unknown role; 409 for a system role or one
		/// that users still have (give them another role first).
		/// </summary>
		[HttpDelete("/api/company/roles/{roleId:int}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> DeleteRole(int roleId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var outcome = await mRoles.DeleteAsync(companyId, roleId, cancellationToken);
			return outcome == RoleAdminOutcome.Done ? NoContent() : RolesProblems.Failure(this, outcome, roleId);
		}

		// ---- access rights ----

		/// <summary>The whole menu as a tree, every item with the flags the role has in this company (false where it has no row).</summary>
		[HttpGet("/api/company/roles/{roleId:int}/menu-permissions")]
		[ProducesResponseType(typeof(RoleMenuPermissionsDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetMenuPermissions(int roleId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var result = await mPermissions.GetAsync(companyId, roleId, cancellationToken);
			return result == null ? RolesProblems.NoSuchRole(this, roleId) : Ok(result);
		}

		/// <summary>
		/// Replaces the role's flags on the menu for this company: for a system role this is the company's override of the defaults.
		/// List every item the role may do anything with; an item left out is not allowed. Ticking create, edit or delete implies view.
		/// 404 for an unknown role, 400 for an unknown or repeated item.
		/// </summary>
		[HttpPut("/api/company/roles/{roleId:int}/menu-permissions")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> SetMenuPermissions(int roleId, [FromBody] SetRoleMenuPermissionsDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return RolesProblems.Result(this, await mPermissions.SetAsync(companyId, roleId, request, cancellationToken), roleId);
		}

		/// <summary>
		/// Drops this company's access rights for the role: a system role goes back to the defaults, an own role is left with none.
		/// Nothing to drop is not an error. 404 for an unknown role.
		/// </summary>
		[HttpDelete("/api/company/roles/{roleId:int}/menu-permissions")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> ResetMenuPermissions(int roleId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return RolesProblems.Result(this, await mPermissions.ResetAsync(companyId, roleId, cancellationToken), roleId);
		}

		// ---- users ----

		/// <summary>The users of the caller's company (deleted ones left out), each with the role they have.</summary>
		[HttpGet("/api/company/roles/users")]
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
		[HttpPost("/api/company/roles/{roleId:int}/members")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> AssignUsers(int roleId, [FromBody] AssignRoleUsersDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var outcome = await mRoles.AssignUsersAsync(companyId, UserId, roleId, request.UserIds ?? [], cancellationToken);
			return outcome == RoleAdminOutcome.Done ? NoContent() : RolesProblems.Failure(this, outcome, roleId);
		}

		private readonly IRoleMenuPermissionsService mPermissions;
		private readonly IRoleAdminService mRoles;
	}
}
