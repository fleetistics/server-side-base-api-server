namespace db_model.Team
{ 

    public class TeamMemberUserStatus
    {
        public const short Creating = 1;
        public const short Active = 2;
        public const short WithOtherTeam = 3;
        public const short Left = 4;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
