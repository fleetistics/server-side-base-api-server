namespace db_model.Contacts
{
    public class ActivityStatus
    {
        public const short Scheduled = 1;
        public const short Completed = 2;
        public const short Cancelled = 3;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
