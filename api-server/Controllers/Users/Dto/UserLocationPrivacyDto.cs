using db_model.UserManagement;

namespace api_server.Controllers.Users.Dto
{
	public class UserLocationPrivacyDto
	{
		public UserLocationPrivacyDto() { }

		public UserLocationPrivacyDto(UserLocationPrivacy model)
		{
			PrivacyMode = model.PrivacyMode;
			LatestUpdate = model.LatestUpdate;
		}

		public short PrivacyMode { get; set; }
		public DateTime LatestUpdate { get; set; }
	}
}
