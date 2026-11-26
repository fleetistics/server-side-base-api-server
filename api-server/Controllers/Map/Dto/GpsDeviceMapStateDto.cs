using db_model.Map;

namespace api_server.Controllers.MapData.Dto
{
    public class GpsDeviceMapStateDto
    {
        public GpsDeviceMapStateDto() { }


        public GpsDeviceMapStateDto(GpsDeviceMapState model)
        {
            GpsDeviceId = model.GpsDeviceId;
            Latitude = model.Location.Y;
            Longitude = model.Location.X;
            Speed = model.Speed;
            Dir = model.Dir;
            ActivityStatusId = model.ActivityStatusId;
            LatestUpdate = model.LatestUpdate;
        }

        public int GpsDeviceId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public short? ActivityStatusId { get; set; }
        public float? Speed { get; set; }
        public short? Dir { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
