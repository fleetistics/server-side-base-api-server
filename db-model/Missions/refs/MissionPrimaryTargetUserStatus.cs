namespace db_model.Missions
{
    public class MissionPrimaryTargetUserStatus
    {
        public const short Active = 1;
        public const short Cancelled = 2;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
