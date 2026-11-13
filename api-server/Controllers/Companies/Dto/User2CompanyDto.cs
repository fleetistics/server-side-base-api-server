using db_model.Companies;

namespace api_server.Companies
{
	public class User2CompanyDto
	{
		public User2CompanyDto() { }

		public User2CompanyDto(User2Company model)
		{
			CompanyId = model.CompanyId;
			UserId = model.UserId;
			PermissionRoleId = model.PermissionRoleId;
			StatusId = model.StatusId;
			JoinTypeId = model.JoinTypeId;
			JoinedDate = model.JoinedDate;
		}

		public int CompanyId { get; set; }
		public int UserId { get; set; }
		public int PermissionRoleId { get; set; }
		public short StatusId { get; set; }
		public short JoinTypeId { get; set; }
		public DateTime JoinedDate { get; set; }
	}
}
