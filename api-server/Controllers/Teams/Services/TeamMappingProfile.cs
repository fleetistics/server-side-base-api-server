using AutoMapper;

namespace api_server.Controllers.Teams
{
	/// <summary>
	/// Team mappings. The CreateTeamDto -> TeamDetails map was removed together with CreateTeamDto
	/// (team creation now goes through TeamCreatePatchDto / ApplyPatch); add maps here as needed.
	/// </summary>
	public class TeamMappingProfile : Profile
	{
	}
}
