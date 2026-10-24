using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Team
{
    public class TeamDetails
    {
        public int TeamId { get; set; }

        public string? IncidentName { get; set; }

        public string? Notes { get; set; }
        public DateTime LatestUpdate { get; set; }

        public List<TeamUploadedMedia>? Medias { get; set; }
        //public Team Team { get; set; }
    }
}
