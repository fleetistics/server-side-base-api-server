namespace db_model.UserManagement
{ 

    public class UserSessionStatus
    {
        public const short JustCreated = 0;
        public const short Active = 1;
        public const short LoggedOutByUser = 2;
        public const short Expired = 3;
        public const short ClosedByConcurrentLogin = 4;
        public const short AccountCancelledByUser = 16;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
