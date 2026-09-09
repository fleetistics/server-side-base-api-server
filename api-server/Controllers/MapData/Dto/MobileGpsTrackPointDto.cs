namespace api_server.Controllers.MapData.Dto
{
	// One location sample reported by the device's own background-geolocation SDK.
	public class MobileGpsTrackPointDto
	{
		public DateTime DeviceDate { get; set; }
		public double Latitude { get; set; }
		public double Longitude { get; set; }
		public float? Speed { get; set; }
		public float? Odometer { get; set; }
		public short? Dir { get; set; }

		// db_model.Map.refs.GPSLocationAccuracy value.
		public short LocationAccuracy { get; set; }

		public float? BatteryLevel { get; set; }
		public bool? BatteryIsCharging { get; set; }

		// db_model.Map.refs.MobileDeviceMotionActivity code (e.g. "still", "on_foot").
		public string? MotionActivity { get; set; }
	}
}
