namespace db_model.Team
{ 

    public class TeamMemberUserStatus
    {
        public const short Active = 1;
        public const short WithOtherTeam = 2;
        public const short Left = 3;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
