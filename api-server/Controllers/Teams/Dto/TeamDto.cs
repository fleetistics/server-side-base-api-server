using db_model.Team;

namespace api_server.Controllers.Teams.Dto
{
	public class TeamDto : TeamHeaderDto
    {
		public TeamDto() { }

		public TeamDto(Team model) : base(model)
        {
		}

        // ******* from TeamDetails
        public void AssignDetails(TeamDetails details)
        {
            Notes = details.Notes;
        }
        public string? Notes { get; set; }
    }
}
