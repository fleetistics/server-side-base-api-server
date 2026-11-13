namespace db_model.Companies
{ 

    public class UserJoinInvitationStatus
    {
        public const short Active = 1;
        public const short Expired = 2;
        public const short Accepted = 3;
        public const short Cancelled = 4;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
