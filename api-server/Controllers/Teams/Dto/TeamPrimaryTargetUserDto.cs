using db_model.Team;

namespace api_server.Controllers.Teams.Dto
{
    public class TeamPrimaryTargetUserDto
    {
        public TeamPrimaryTargetUserDto() { }

        public TeamPrimaryTargetUserDto(TeamPrimaryTargetUser model)
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
}
