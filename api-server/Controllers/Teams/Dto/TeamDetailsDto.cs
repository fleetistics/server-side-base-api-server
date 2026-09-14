using db_model.Team;

namespace api_server.Controllers.Teams.Dto
{
	public class TeamDetailsDto 
    {
		public TeamDetailsDto() { }

		public TeamDetailsDto(TeamDetails details)
        {
            Notes = details.Notes;
        }
        public string? Notes { get; set; }
    }
}
