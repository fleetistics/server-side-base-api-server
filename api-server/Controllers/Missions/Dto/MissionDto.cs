using db_model.Missions;

namespace api_server.Missions
{
    public class MissionDto
    {
        public MissionDto() { }

        public MissionDto(Mission model)
        {
            Id = model.Id;
            CreatorUserId = model.CreatorUserId;
            CompanyId = model.CompanyId;
            Name = model.Name;
            StatusId = model.StatusId;
            CreatedDate = model.CreatedDate;
            ClosedDate = model.ClosedDate;
            JoinKey = model.JoinKey;
            JoinQrCode = model.JoinQrCode;
        }

        public int? Id { get; set; }
        public int CreatorUserId { get; set; }
        public int? CompanyId { get; set; }
        public string Name { get; set; } = default!;
        public short? StatusId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public string? JoinKey { get; set; }
        public string? JoinQrCode { get; set; }
    }
}
