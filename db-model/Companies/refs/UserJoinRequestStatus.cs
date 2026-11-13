namespace db_model.Companies
{ 

    public class UserJoinRequestStatus
    {
        public const short RequestedByUser = 1;
        public const short AutoAccepted = 2;
        public const short Accepted = 3;
        public const short Rejected = 4;
        public const short Cancelled = 5;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
