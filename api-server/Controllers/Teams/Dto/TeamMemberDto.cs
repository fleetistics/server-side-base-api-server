using db_model.Team;

namespace api_server.Controllers.Teams.Dto
{
	public class TeamMemberDto
	{
		public TeamMemberDto() { }

		public TeamMemberDto(TeamMemberUser model)
		{
			UserId = model.UserId;
			StatusId = model.StatusId;
			LatestUpdate = model.LatestUpdate;
		}

		public int UserId { get; set; }
		public short StatusId { get; set; }
		public DateTime LatestUpdate { get; set; }
	}
}
