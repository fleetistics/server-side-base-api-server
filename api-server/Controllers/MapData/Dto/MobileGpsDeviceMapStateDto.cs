using db_model.Map;

namespace api_server.Controllers.MapData.Dto
{
    public class MobileGpsDeviceMapStateDto
    {
        public MobileGpsDeviceMapStateDto() { }

        public MobileGpsDeviceMapStateDto(MobileGpsDeviceMapState model)
        {
            MobileGpsDeviceId = model.MobileGpsDeviceId;
            Latitude = model.Location.Y;
            Longitude = model.Location.X;
            MotionActivity = model.MotionActivity;
            Speed = model.Speed;
            Dir = model.Dir;
            BatteryLevel = model.BatteryLevel;
            LatestOnMapUpdate = model.LatestOnMapUpdate;
        }

        public int MobileGpsDeviceId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public short? MotionActivity { get; set; }
        public float? Speed { get; set; }
        public short? Dir { get; set; }
        public float? BatteryLevel { get; set; }
        public DateTime LatestOnMapUpdate { get; set; }
    }
}
