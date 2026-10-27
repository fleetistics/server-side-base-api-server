namespace db_model.UserManagement
{ 

    public class UserSource
    {
        public const short AUTO_DeviceUID = 1;
        public const short Registered = 2;

        public short Id { get; set; }
        public string Name { get; set; } = default!;

        public DateTime LatestUpdate { get; set; }
    }
}
