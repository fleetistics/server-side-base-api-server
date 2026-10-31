namespace db_model.Gps
{
    /// <summary>
    /// What kind of device a <see cref="GpsDevice"/> is: a phone platform (Android, iOS) or a tracker model
    /// (Geotab GO9 ...). A tree (<see cref="ParentId"/>) so a family can group its models. Mobile types are
    /// seeded with the constants below; Geotab types are created by the feeders' asset sync from Geotab's own
    /// device-type string, so they have no constants.
    /// </summary>
    public class GpsDeviceType
    {
        public const int MobileAndroid = 1;
        public const int MobileIos = 2;

        public int Id { get; set; }
        public string Name { get; set; } = default!;

        /// <summary><see cref="GpsDeviceProvider"/> id; a device's own ProviderId must equal its type's.</summary>
        public int ProviderId { get; set; }
        public int? ParentId { get; set; }

        public DateTime LatestUpdate { get; set; }
    }
}
