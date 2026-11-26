using db_model.UserManagement;

namespace api_server.Users
{
	public class ActiveUserEmergencyAlertDto
	{
		public ActiveUserEmergencyAlertDto() { }

		public ActiveUserEmergencyAlertDto(UserEmergencyAlert model)
		{
			UserId = model.UserId;
            GpsDeviceId = model.GpsDeviceId;
			CreatedDate = model.CreatedDate;
			Latitude = model.Location.X;
			Longitude = model.Location.Y;
		}
        public int UserId { get; set; }
        public int? GpsDeviceId { get; set; }
        public DateTime CreatedDate { get; set; }
		public double? Latitude { get; set; }
		public double? Longitude { get; set; }
	}
}
