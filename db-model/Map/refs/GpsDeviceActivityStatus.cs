namespace db_model.Map
{
    /// <summary>What a device is doing: <see cref="GpsDeviceMapState.ActivityStatusId"/>.</summary>
    public class GpsDeviceActivityStatus
    {
        public const byte Unknown = 0;
        public const byte Driving = 1;
        public const byte Idle = 2;
        public const byte Stopped = 3;

        public const byte Walking = 5;
        public const byte Running = 6;
        public const byte OnBicycle = 7;

        public byte Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
