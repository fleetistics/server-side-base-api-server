namespace db_model.Team
{ 

    public class TeamUserRole
    {
        public const short Creator = 1;
        public const short Lead = 2;
        public const short Member = 3;
        public const short Viewer = 4;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
