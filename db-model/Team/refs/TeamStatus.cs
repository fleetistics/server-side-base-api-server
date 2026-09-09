namespace db_model.Team
{ 

    public class TeamStatus
    {
        public const short Creating = 1;
        public const short Active = 2;
        public const short Completed = 3;
        public const short Cancelled = 4;
        public const short Expired = 5;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
