using db_model.Team;

namespace api_server.Teams
{
	public class TeamDetailsDto 
    {
		public TeamDetailsDto() { }

		public TeamDetailsDto(TeamDetails details)
        {
            Notes = details.Notes;
            IncidentName = details.IncidentName;
        }
        public string? Notes { get; set; }
        public string? IncidentName { get; set; }
        public List<TeamMediaDto>? Medias { get; set; }
    }
}
