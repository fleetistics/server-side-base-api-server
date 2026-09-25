

namespace db_model.Messages
{
    public class UserMessage2User
    {
        public int UserMessageId { get; set; }
        public int UserId { get; set; }
        public short StatusId { get; set; }
        public DateTime LatestUpdate { get; set; }

    }
}
