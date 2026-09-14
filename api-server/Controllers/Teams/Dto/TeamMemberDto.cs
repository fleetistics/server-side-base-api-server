using db_model.Team;

namespace api_server.Controllers.Teams.Dto
{
	public class TeamMemberDto
	{
		public TeamMemberDto() { }

		public TeamMemberDto(TeamMemberUser model)
		{
			UserId = model.UserId;
            RoleId = model.RoleId;
		}

		public int UserId { get; set; }
		public short RoleId { get; set; }

	}
}
