namespace api_server.Controllers.Users.Dto
{
	public class UserSettingsPutDto
	{
		public string Name { get; set; }
		public string? Value { get; set; }
		public short? ClientApplicationId { get; set; }
		public short? ClientDevicePlatformId { get; set; }
	}
}
