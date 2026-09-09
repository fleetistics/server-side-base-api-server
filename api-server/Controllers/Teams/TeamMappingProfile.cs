using api_server.Controllers.Teams.Dto;
using AutoMapper;
using db_model.Team;

namespace api_server.Controllers.Teams
{
	/// <summary>
	/// Base project defines only the common fields (none currently overlap between
	/// CreateTeamDto and TeamDetails). An app-specific fork adds its own matching
	/// properties to both classes and they'll be copied automatically here by
	/// convention (same name/type) - no mapping code to update per app.
	/// </summary>
	public class TeamMappingProfile : Profile
	{
		public TeamMappingProfile()
		{
			CreateMap<CreateTeamDto, TeamDetails>();
		}
	}
}
