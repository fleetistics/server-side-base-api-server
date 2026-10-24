using api_server.Users;

namespace api_server.Teams
{
    public class TeamCreateDto
    {
        public TeamDto? Team { get; set; }
        public TeamDetailsDto? TeamDetails { get; set; }
        //public UserDto? CreatorUser { get; set; }
        public TeamMemberUserDto? CreatorTeamMemberUser { get; set; }
    }
}
