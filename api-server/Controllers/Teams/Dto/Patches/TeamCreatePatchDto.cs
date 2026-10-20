using api_server.Users;

namespace api_server.Teams
{

	public class TeamCreatePatchDto
	{
		public TeamPatchDto? Team { get; set; }
		public TeamDetailsPatchDto? TeamDetails { get; set; }
		public UserPatchDto? CreatorUser { get; set; }
		public TeamMemberUserPatchDto? CreatorTeamMemberUser { get; set; }
	}
}
