using exs.commons.Json;

namespace api_server.Teams
{

	public class TeamMemberUserPatchDto
	{
		public Optional<short?> IconKey { get; set; }
        public Optional<short?> RoleId { get; set; }
    }
}
