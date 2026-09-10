using db_model.UserManagement;

namespace api_server.Controllers.Users.Dto
{
	public class MyUserEmergencyAlertDto
	{
		public MyUserEmergencyAlertDto() { }

		public MyUserEmergencyAlertDto(UserEmergencyAlert model)
		{
			CreatedDate = model.CreatedDate;
			Latitude = model.Latitude;
			Longitude = model.Longitude;
		}

		public DateTime CreatedDate { get; set; }
		public float? Latitude { get; set; }
		public float? Longitude { get; set; }
	}
}
