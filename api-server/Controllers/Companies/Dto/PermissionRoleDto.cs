using db_model.Companies;

namespace api_server.Companies
{
	public class PermissionRoleDto
	{
		public PermissionRoleDto() { }

		public PermissionRoleDto(PermissionRole model)
		{
			Id = model.Id;
			Name = model.Name;
			CompanyId = model.CompanyId;
			IsDefault = model.IsDefault;
			Description = model.Description;
		}

		public int Id { get; set; }
		public string Name { get; set; } = default!;
		public int? CompanyId { get; set; }
		public bool? IsDefault { get; set; }
		public string? Description { get; set; }
	}
}
