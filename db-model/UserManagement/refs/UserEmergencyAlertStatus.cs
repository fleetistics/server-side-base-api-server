namespace db_model.UserManagement
{ 

    public class UserEmergencyAlertStatus
    {
        public const short JustCreated = 0;
        public const short Active = 1;
        public const short ColosedByUser = 2;
        public const short Expired = 3;
        public const short Cancelled = 13;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
