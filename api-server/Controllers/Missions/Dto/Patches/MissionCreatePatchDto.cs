using api_server.Users;

namespace api_server.Missions
{
	public class MissionCreatePatchDto
	{
		public MissionPatchDto? Mission { get; set; } 
		public MissionDetailsPatchDto? MissionDetails { get; set; }
		public UserPatchDto? CreatorUser { get; set; }
	}
}
