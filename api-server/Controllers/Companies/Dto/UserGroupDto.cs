using db_model.Companies;

namespace api_server.Companies
{
	public class UserGroupDto
	{
		public UserGroupDto() { }

		public UserGroupDto(UserGroup model)
		{
			Id = model.Id;
			Name = model.Name;
			CompanyId = model.CompanyId;
			GroupColor = model.GroupColor;
			Description = model.Description;
		}

		public int Id { get; set; }
		public string Name { get; set; } = default!;
		public int CompanyId { get; set; }
		public short GroupColor { get; set; }
		public string? Description { get; set; }
	}
}
