using db_model.UserManagement;

namespace api_server.Controllers.Users.Dto
{
	public class ActiveUserEmergencyAlertDto
	{
		public ActiveUserEmergencyAlertDto() { }

		public ActiveUserEmergencyAlertDto(UserEmergencyAlert model)
		{
			UserId = model.UserId;
			MobileGpsDeviceId = model.MobileGpsDeviceId;
			CreatedDate = model.CreatedDate;
			Latitude = model.Latitude;
			Longitude = model.Longitude;
		}
        public int UserId { get; set; }
        public int? MobileGpsDeviceId { get; set; }
        public DateTime CreatedDate { get; set; }
		public float? Latitude { get; set; }
		public float? Longitude { get; set; }
	}
}
