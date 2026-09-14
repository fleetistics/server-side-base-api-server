using db_model.Gps;

namespace api_server.BusinessLogic.Dto
{
    public class MobileGpsDeviceDto
    {
        public MobileGpsDeviceDto() { }

        public MobileGpsDeviceDto(MobileGpsDevice model)
        {
            Id = model.Id;
            UserId = model.UserId!.Value;
            Name = model.Name;
        }

        public int Id { get; set; }
        public int UserId { get; set; }

        public string? Name { get; set; }
    }
}
