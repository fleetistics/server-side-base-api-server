using db_model.Missions;

namespace api_server.Missions
{
    public class MissionDetailsDto
    {
        public MissionDetailsDto() { }

        public MissionDetailsDto(MissionDetails details)
        {
            Notes = details.Notes;
            IncidentName = details.IncidentName;
            Medias = details.Medias?.Select(m => new MissionMediaDto(m)).ToList();
        }
        public string? Notes { get; set; }
        public string? IncidentName { get; set; }
        public List<MissionMediaDto>? Medias { get; set; }
    }
}
