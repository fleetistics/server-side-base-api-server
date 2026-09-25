namespace db_model.Team
{ 

    public class TeamActivityType
    {
        public const short MemberJoined = 1;
        public const short MemberLeft = 2;
        public const short MemberSwitchedOut = 3;
        public const short MemberSwitchedIn = 4;

        public const short MemberReport = 5;


        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
