using db_model.UserManagement;

namespace api_server.Users
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
