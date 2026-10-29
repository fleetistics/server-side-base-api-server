namespace db_model.Gps
{
    /// <summary>
    /// What a device is attached to or carried by (a car, a truck, a person with a phone ...), which the map uses to pick
    /// an icon. Not the hardware: that is <see cref="GpsDeviceType"/>. Set on <see cref="GpsDevice.AssetTypeId"/>.
    /// </summary>
    public class GpsAssetType
    {
        public const int Phone = 1;
        public const int Car = 2;
        public const int Pickup = 3;
        public const int Truck = 4;
        public const int Van = 5;
        public const int Bus = 6;
        public const int Motorcycle = 7;
        public const int Trailer = 8;
        public const int Equipment = 9;
        public const int Other = 10;

        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
