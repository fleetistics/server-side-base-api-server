using db_model.Missions;

namespace api_server.Missions
{
    public class MissionPrimaryTargetUserDto
    {
        public MissionPrimaryTargetUserDto() { }

        public MissionPrimaryTargetUserDto(MissionPrimaryTargetUser model)
        {
            TargetUserId = model.TargetUserId;
            UpdatedByUserId = model.UpdatedByUserId;
            StatusId = model.StatusId;
            LatestUpdate = model.LatestUpdate;
        }

        public int? TargetUserId { get; set; }
        public int? UpdatedByUserId { get; set; }
        public short StatusId { get; set; }
        public DateTime LatestUpdate { get; set; }
    }

    /// <summary>Body of PUT /api/mission/{missionId}/primary-target-user.</summary>
    public class MissionPrimaryTargetUserAssignDto
    {
        public int? TargetUserId { get; set; }
    }
}
