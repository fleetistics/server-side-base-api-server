using db_model.Companies;

namespace api_server.Companies
{
	public class User2GroupDto
	{
		public User2GroupDto() { }

		public User2GroupDto(User2Group model)
		{
			CompanyId = model.CompanyId;
			UserId = model.UserId;
			GroupId = model.GroupId;
		}

		public int CompanyId { get; set; }
		public int UserId { get; set; }
		public int GroupId { get; set; }
	}
}
