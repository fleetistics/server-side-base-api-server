namespace db_model.Gps
{
    /// <summary>
    /// The location privacy of one GPS device, the device counterpart of <c>UserLocationPrivacy</c>: 0 = not private, 1 = private.
    /// The feeders set it when the device's private mode changes in the provider (Geotab trip type Private).
    /// </summary>
    public class GpsDeviceLocationPrivacy
    {
        public short PrivacyMode { get; set; }
        public int GpsDeviceId { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
