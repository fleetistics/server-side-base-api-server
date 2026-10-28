namespace db_model.Map
{
    public class GpsDeviceMapStatus
    {
        public const byte Unknown = 0;
        public const byte Moving = 1;
        public const byte Idle = 2;
        public const byte Parked = 3;
        public const byte Offline = 4;

        public byte Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
