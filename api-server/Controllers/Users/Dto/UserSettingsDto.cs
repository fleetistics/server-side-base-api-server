using db_model.UserManagement;

namespace api_server.Controllers.Users.Dto
{
	public class UserSettingsDto
	{
		public UserSettingsDto() { }

		public UserSettingsDto(UserSettings model)
		{
			Name = model.Name;
			Value = model.Value;
		}

		public string Name { get; set; }
		public string? Value { get; set; }
	}
}
