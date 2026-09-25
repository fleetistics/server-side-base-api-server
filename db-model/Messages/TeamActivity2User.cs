

namespace db_model.Messages
{
    public class TeamActivity2User
    {
        public int TeamActivityId { get; set; }
        public int UserId { get; set; }
        public short StatusId { get; set; }
        public DateTime LatestUpdate { get; set; }

    }
}
