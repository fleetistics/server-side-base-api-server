using api_server.Controllers.Base;
using api_server.Controllers.Roles.Dto;
using api_server.Controllers.Roles.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Roles
{
	/// <summary>
	/// The Portal Admin ▸ Roles screen: the default access rights of the system roles (the roles every company can use), i.e. which
	/// menu items each may use in a company that has not overridden that role. Nothing else is edited here; a company's own roles, its
	/// overrides and who has which role are in <see cref="CompanyRolesController"/>.
	///
	/// NOTE: access is not restricted by role yet. Any signed-in user can call these endpoints, and a change applies to every
	/// company without an override. The menu only hides the page. Deciding who may call this is still open.
	///
	/// Only view is enforced (it decides what GET /api/menu returns); create, edit and delete are stored and nothing else yet.
	/// </summary>
	public class RolesAdminController : AuthAPIController
	{
		public RolesAdminController(IRoleMenuPermissionsService permissions)
		{
			mPermissions = permissions;
		}

		/// <summary>The system roles, each with how much of the menu it may use by default.</summary>
		[HttpGet("/api/admin/roles")]
		[ProducesResponseType(typeof(IReadOnlyList<RoleSummaryDto>), StatusCodes.Status200OK)]
		public async Task<IActionResult> GetRoles(CancellationToken cancellationToken) =>
			Ok(await mPermissions.ListRolesAsync(null, cancellationToken));

		/// <summary>The whole menu as a tree, every item with the role's default flags (false where the role has no row).</summary>
		[HttpGet("/api/admin/roles/{roleId:int}/menu-permissions")]
		[ProducesResponseType(typeof(RoleMenuPermissionsDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetMenuPermissions(int roleId, CancellationToken cancellationToken)
		{
			var result = await mPermissions.GetAsync(null, roleId, cancellationToken);
			return result == null ? RolesProblems.NoSuchRole(this, roleId) : Ok(result);
		}

		/// <summary>
		/// Replaces the role's default flags on the menu. List every item the role may do anything with; an item left out is not allowed.
		/// Ticking create, edit or delete implies view. 404 for an unknown or non-system role, 400 for an unknown or repeated item.
		/// </summary>
		[HttpPut("/api/admin/roles/{roleId:int}/menu-permissions")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> SetMenuPermissions(int roleId, [FromBody] SetRoleMenuPermissionsDto request, CancellationToken cancellationToken) =>
			RolesProblems.Result(this, await mPermissions.SetAsync(null, roleId, request, cancellationToken), roleId);

		private readonly IRoleMenuPermissionsService mPermissions;
	}
}
