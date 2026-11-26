using db_model.UserManagement;

namespace api_server.Users
{
	public class MyUserEmergencyAlertDto
	{
		public MyUserEmergencyAlertDto() { }

		public MyUserEmergencyAlertDto(UserEmergencyAlert model)
		{
			CreatedDate = model.CreatedDate;
            GpsDeviceId = model.GpsDeviceId;
			Latitude = model.Location.X;
			Longitude = model.Location.Y;
		}

		public DateTime CreatedDate { get; set; }
		public int? GpsDeviceId { get; set; }
		public double? Latitude { get; set; }
		public double? Longitude { get; set; }
	}
}
