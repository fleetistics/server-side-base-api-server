

namespace db_model.Messages
{
    
    public class UserMessageStatus
    {
        public const short Active = 1;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
