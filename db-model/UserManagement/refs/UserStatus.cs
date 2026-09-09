namespace db_model.UserManagement
{ 

    public class UserStatus
    {
        public const byte JustCreated = 0;
        public const byte Active = 1;
        public const byte Inactive = 13;

        public byte Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
