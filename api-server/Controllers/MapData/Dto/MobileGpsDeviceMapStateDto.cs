using db_model.Map;

namespace api_server.Controllers.MapData.Dto
{
    public class MobileGpsDeviceMapStateDto
    {
        public MobileGpsDeviceMapStateDto() { }

        /// <summary>
        /// Same wire shape as before the phones moved to gps_device_map_state: Speed goes back to m/s (the table keeps
        /// km/h), motion activity and battery come from the state data JSON.
        /// </summary>
        public MobileGpsDeviceMapStateDto(GpsDeviceMapState model, int userId, string? stateData)
        {
            MobileGpsDeviceId = model.GpsDeviceId;
            UserId = userId;
            Latitude = model.Location.Y;
            Longitude = model.Location.X;
            Speed = model.Speed / Services.PhoneMapState.KmhPerMs;
            Dir = model.Dir;
            (MotionActivity, BatteryLevel) = Services.PhoneMapState.ReadValues(stateData);
            LatestOnMapUpdate = model.LatestOnMapUpdate;
        }

        public int MobileGpsDeviceId { get; set; }
        public int UserId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public short? MotionActivity { get; set; }
        public float? Speed { get; set; }
        public short? Dir { get; set; }
        public float? BatteryLevel { get; set; }
        public DateTime LatestOnMapUpdate { get; set; }
    }
}
