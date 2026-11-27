namespace db_model.Contacts
{
    public class ActivityPriority
    {
        public const short Low = 1;
        public const short Normal = 2;
        public const short High = 3;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
