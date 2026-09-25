
namespace db_model.Messages
{
    public class UserMessage
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public int? ToUserId { get; set; }
        public int? TeamId { get; set; }
        public string Message { get; set; } = default!;
        public short StatusId { get; set; }
        public DateTime LatestUpdate { get; set; }

        public List<UserMessage2User> UserMessage2Users { get; set; } = default!;
    }
}
