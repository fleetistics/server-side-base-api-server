namespace api_server.Controllers.Users.Dto
{
	/// <summary>
	/// Body for POST /api/users/me/emergency-alert/complete. CompletedDate and StatusId are
	/// derived/assigned server-side - only the device's location at completion time (if it has a
	/// fix) is client-supplied.
	/// </summary>
	public class CompleteUserEmergencyAlertDto
	{
		public float? CompleteLatitude { get; set; }
		public float? CompleteLongitude { get; set; }
	}
}
