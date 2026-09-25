
namespace db_model.Messages
{ 
  
    public class UserMessage2UserStatus
    {
        public const short Read = 1;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
