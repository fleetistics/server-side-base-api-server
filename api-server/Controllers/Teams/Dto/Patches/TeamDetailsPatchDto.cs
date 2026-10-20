using api_server.Medias;
using exs.commons.Json;

namespace api_server.Teams
{
	public class TeamDetailsPatchDto: InboundMediaAttachableDto
    {
		public Optional<string?> IncidentName { get; set; }
        public Optional<string?> Notes { get; set; }
    }
}
