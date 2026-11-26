using db_model.Gps;

namespace api_server.Fleet
{
    public class GpsDeviceDto
    {
        public GpsDeviceDto() { }

        public GpsDeviceDto(GpsDevice model)
        {
            Id = model.Id;
            UserId = model.UserId;
            Name = model.Name;
        }

        public int Id { get; set; }
        public int? UserId { get; set; }

        public string? Name { get; set; }
    }
}
