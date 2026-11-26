using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Missions
{
    public class MissionDetails
    {
        public int MissionId { get; set; }

        public string? IncidentName { get; set; }

        public string? Notes { get; set; }
        public DateTime LatestUpdate { get; set; }

        public List<MissionUploadedMedia>? Medias { get; set; }
        //public Team Team { get; set; }
    }
}
