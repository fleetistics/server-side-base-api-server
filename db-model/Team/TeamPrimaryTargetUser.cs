using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Team
{
    public class TeamPrimaryTargetUser
    {
        public int TeamId { get; set; }

        public int? TargetUserId { get; set; }
        public int? UpdatedByUserId { get; set; }
        public short StatusId { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
