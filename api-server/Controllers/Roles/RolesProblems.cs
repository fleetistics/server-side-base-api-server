using api_server.Controllers.Roles.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Roles
{
	/// <summary>The error answers the two roles controllers share.</summary>
	internal static class RolesProblems
	{
		public static ObjectResult NoSuchRole(ControllerBase controller, int? roleId) =>
			controller.Problem(title: "Not Found", detail: roleId is int id ? $"There is no role with id {id} that this screen can use." : "There is no such role that this screen can use.", statusCode: StatusCodes.Status404NotFound);

		/// <summary>204 for a stored change, otherwise the matching problem.</summary>
		public static IActionResult Result(ControllerBase controller, SetRoleMenuPermissionsOutcome outcome, int roleId) => outcome switch
		{
			SetRoleMenuPermissionsOutcome.Updated => controller.NoContent(),
			SetRoleMenuPermissionsOutcome.RoleNotFound => NoSuchRole(controller, roleId),
			SetRoleMenuPermissionsOutcome.UnknownMenuItem => controller.Problem(title: "Unknown menu item", detail: "An entry names a menu item that is not in the menu.", statusCode: StatusCodes.Status400BadRequest),
			SetRoleMenuPermissionsOutcome.DuplicateMenuItem => controller.Problem(title: "Repeated menu item", detail: "Two entries name the same menu item.", statusCode: StatusCodes.Status400BadRequest),
			_ => throw new InvalidOperationException("Unhandled outcome"),
		};

		public static ObjectResult Failure(ControllerBase controller, RoleAdminOutcome outcome, int? roleId = null) => outcome switch
		{
			RoleAdminOutcome.RoleNotFound => NoSuchRole(controller, roleId),
			RoleAdminOutcome.SourceRoleNotFound => controller.Problem(title: "Unknown role to copy", detail: "The role to copy the access rights from is not one this company can use.", statusCode: StatusCodes.Status400BadRequest),
			RoleAdminOutcome.InvalidName => controller.Problem(title: "Invalid name", detail: $"A role name has 1 to {RoleAdminRules.MaxNameLength} characters.", statusCode: StatusCodes.Status400BadRequest),
			RoleAdminOutcome.NameTaken => controller.Problem(title: "Name taken", detail: "Another role this company can use already has that name.", statusCode: StatusCodes.Status409Conflict),
			RoleAdminOutcome.SystemRole => controller.Problem(title: "System role", detail: "A role every company uses cannot be renamed or deleted; give it access rights and members instead.", statusCode: StatusCodes.Status409Conflict),
			RoleAdminOutcome.HasMembers => controller.Problem(title: "Role in use", detail: "Users still have this role. Give them another role first.", statusCode: StatusCodes.Status409Conflict),
			RoleAdminOutcome.CannotChangeSelf => controller.Problem(title: "Cannot change your own role", detail: "Nobody can change their own role here, so that nobody can lock themselves out.", statusCode: StatusCodes.Status409Conflict),
			RoleAdminOutcome.UserNotFound => controller.Problem(title: "Unknown user", detail: "A user in the list is not a member of this company.", statusCode: StatusCodes.Status400BadRequest),
			RoleAdminOutcome.MemberNotEditable => controller.Problem(title: "User cannot be changed", detail: "A user in the list was suspended by the site admin; only the site admin can change them.", statusCode: StatusCodes.Status409Conflict),
			_ => throw new InvalidOperationException("Unhandled outcome"),
		};
	}
}
