using db_model.Companies;

namespace api_server.Controllers.Roles.Services
{
	/// <summary>The rules for creating and renaming roles that need no database.</summary>
	public static class RoleAdminRules
	{
		public const int MaxNameLength = 100;

		/// <summary>The name without the spaces around it; null when there is nothing left.</summary>
		public static string? NormalizeName(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();

		public static bool IsValidName(string? normalizedName) => normalizedName != null && normalizedName.Length <= MaxNameLength;

		/// <summary>Text for a description: trimmed, and null when empty (the column is null for "none").</summary>
		public static string? NormalizeDescription(string? description) => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

		/// <summary>Whether a name is already taken (ignoring case) by one of the roles the company can use, other than <paramref name="exceptRoleId"/>.</summary>
		public static bool IsNameTaken(IEnumerable<PermissionRole> usableRoles, string name, int? exceptRoleId = null) =>
			usableRoles.Any(r => r.Id != exceptRoleId && string.Equals(r.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));

		/// <summary>
		/// Memberships that are gone as far as the admin screens are concerned (deleted), and the one the site admin suspended, which nobody
		/// else may change.
		/// </summary>
		public static bool IsDeleted(short statusId) => statusId is User2CompanyStatus.Deleted or User2CompanyStatus.DeletedBySiteAdmin;

		public static bool IsLockedBySiteAdmin(short statusId) => statusId == User2CompanyStatus.SuspendedBySiteAdmin;
	}
}
