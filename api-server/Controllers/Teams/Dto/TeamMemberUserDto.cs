using db_model.Team;

namespace api_server.Controllers.Teams.Dto
{
	public class TeamMemberUserDto
	{
		public TeamMemberUserDto() { }

		public TeamMemberUserDto(TeamMemberUser model)
		{
			UserId = model.UserId;
            RoleId = model.RoleId;
            IconKey = model.IconKey;

        }

		public int UserId { get; set; }
		public short? RoleId { get; set; }
        public short? IconKey { get; set; }

    }
}
