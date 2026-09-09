using db_model.Team;

namespace api_server.Controllers.Teams.Dto
{
	/// <summary>
	/// Expanded "team details" view: the core team fields plus its concurrency stamp
	/// (backed by the team_details companion table) and its related members/media.
	/// </summary>
	public class TeamDetailsDto
	{
		public TeamDetailsDto() { }

		public TeamDetailsDto(Team team, DateTime latestUpdate, IReadOnlyList<TeamMemberUser> members, IReadOnlyList<TeamUploadedMedia> media)
		{
			Team = new TeamDto(team);
			LatestUpdate = latestUpdate;
			Members = members.Select(e => new TeamMemberDto(e)).ToList();
			Media = media.Select(e => new TeamMediaDto(e)).ToList();
		}

		public TeamDto Team { get; set; } = default!;
		public DateTime LatestUpdate { get; set; }
		public List<TeamMemberDto> Members { get; set; } = [];
		public List<TeamMediaDto> Media { get; set; } = [];
	}
}
