

namespace api_server.Missions
{
	/// <summary>A mission being set up, with its details and primary target user.</summary>
	public class CreatingMissionDto
	{
		public MissionDto Mission { get; set; } = default!;
		public MissionDetailsDto? MissionDetails { get; set; }
		//public MissionPrimaryTargetUserDto? PrimaryTargetUser { get; set; }
	}
}
