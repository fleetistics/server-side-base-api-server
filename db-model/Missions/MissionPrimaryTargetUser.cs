using System;

namespace db_model.Missions
{
    /// <summary>The user a mission is aimed at: one row per mission (MissionId is the key). Rebuilt from TeamPrimaryTargetUser and the model snapshot.</summary>
    public class MissionPrimaryTargetUser
    {
        public int MissionId { get; set; }

        public int? TargetUserId { get; set; }
        public int? UpdatedByUserId { get; set; }
        public short StatusId { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
