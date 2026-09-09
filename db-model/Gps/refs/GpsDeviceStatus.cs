namespace db_model.Gps
{ 

    public class GpsDeviceStatus
    {
        public const byte Active = 1;
        public const byte Inactive = 13;

        public byte Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
