

namespace db_model.Team
{
    public class TeamMemberUser
    {
        public int TeamId { get; set; }
        public int UserId { get; set; }
        public short StatusId { get; set; }
        public DateTime LatestUpdate { get; set; }


    }
}
