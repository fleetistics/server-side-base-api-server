namespace api_server.Controllers.Roles.Dto
{
	/// <summary>Body of POST /api/admin/roles: a new role of the caller's company.</summary>
	public class CreateRoleDto
	{
		/// <summary>1 to 100 characters, different (ignoring case) from every role the company can already use.</summary>
		public string? Name { get; set; }

		public string? Description { get; set; }

		/// <summary>A role (one the company can use) whose access rights the new role starts with; null starts with none. This is also how a role is duplicated.</summary>
		public int? CopyFromRoleId { get; set; }
	}

	/// <summary>Body of PATCH /api/admin/roles/{roleId}. A field that is left out (null) is not changed; an empty Description clears it.</summary>
	public class UpdateRoleDto
	{
		public string? Name { get; set; }

		public string? Description { get; set; }
	}

	/// <summary>A user of the caller's company, with the role they have there.</summary>
	public class RoleUserDto
	{
		public int UserId { get; set; }

		public string DisplayName { get; set; } = default!;

		public string? Email { get; set; }

		/// <summary>The membership's status (db_model User2CompanyStatus: 1 Active, 2 Suspended, ...).</summary>
		public short StatusId { get; set; }

		public int RoleId { get; set; }

		/// <summary>The caller: they cannot be moved to another role (nobody can lock themselves out).</summary>
		public bool IsCurrentUser { get; set; }
	}

	/// <summary>Body of POST /api/admin/roles/{roleId}/members: these users get the role (each user has exactly one, so it replaces their current one).</summary>
	public class AssignRoleUsersDto
	{
		public List<int> UserIds { get; set; } = [];
	}
}
