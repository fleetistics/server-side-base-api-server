namespace api_server.Controllers.Users.Dto
{
	/// <summary>
	/// Body for POST /api/users/me/emergency-alert. UserId, Id, CreatedDate and StatusId are all
	/// derived/assigned server-side - only the reporting device's location (if it has a fix at
	/// the moment the alert is raised) is client-supplied.
	/// </summary>
	public class CreateUserEmergencyAlertDto
	{
		public float? Latitude { get; set; }
		public float? Longitude { get; set; }
	}
}
