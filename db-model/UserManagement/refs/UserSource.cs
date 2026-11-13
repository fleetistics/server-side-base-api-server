namespace db_model.UserManagement
{ 

    public class UserSource
    {
        public const short AUTO_DeviceUID = 1;
        public const short Registration_Portal = 2;
        public const short Registration_App = 3;
        public const short MF_Migrate = 4;
        public const short Manual = 5;
        public const short GPS_Test_User = 13;
        public const short Dev_User = 12;
        public const short Fake_User = 14;

        public short Id { get; set; }
        public string Name { get; set; } = default!;

        public DateTime LatestUpdate { get; set; }
    }
}
