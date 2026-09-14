using db_model.UserManagement;

namespace api_server.Controllers.Users.Dto
{
	public class UserEmergencyAlertDto
	{
		public UserEmergencyAlertDto() { }

		public UserEmergencyAlertDto(UserEmergencyAlert model)
		{
			Id = model.Id;
			UserId = model.UserId;
			MobileGpsDeviceId = model.MobileGpsDeviceId;
			CreatedDate = model.CreatedDate;
			CompletedDate = model.CompletedDate;
			Latitude = model.Latitude;
			Longitude = model.Longitude;
			CompleteLatitude = model.CompleteLatitude;
			CompleteLongitude = model.CompleteLongitude;
			StatusId = model.StatusId;
			LatestUpdate = model.LatestUpdate;
		}

		public int Id { get; set; }
		public int UserId { get; set; }
		public int? MobileGpsDeviceId { get; set; }
		public DateTime CreatedDate { get; set; }
		public DateTime? CompletedDate { get; set; }
		public float? Latitude { get; set; }
		public float? Longitude { get; set; }
		public float? CompleteLatitude { get; set; }
		public float? CompleteLongitude { get; set; }
		public short StatusId { get; set; }
		public DateTime LatestUpdate { get; set; }
	}
}
