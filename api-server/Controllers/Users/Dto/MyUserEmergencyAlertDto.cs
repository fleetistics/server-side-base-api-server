using db_model.UserManagement;

namespace api_server.Controllers.Users.Dto
{
	public class MyUserEmergencyAlertDto
	{
		public MyUserEmergencyAlertDto() { }

		public MyUserEmergencyAlertDto(UserEmergencyAlert model)
		{
			CreatedDate = model.CreatedDate;
			MobileGpsDeviceId = model.MobileGpsDeviceId;
			Latitude = model.Latitude;
			Longitude = model.Longitude;
		}

		public DateTime CreatedDate { get; set; }
		public int? MobileGpsDeviceId { get; set; }
		public double? Latitude { get; set; }
		public double? Longitude { get; set; }
	}
}
