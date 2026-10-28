namespace db_model.Map
{
    /// <summary>
    /// Latest whitelisted diagnostic values of a GPS device, one row per device. Split from
    /// <see cref="GpsDeviceMapState"/> because it is written on a slower cadence than the position.
    /// </summary>
    public class GpsDeviceStateData
    {
        public int GpsDeviceId { get; set; }

        /// <summary>JSON object: key -> { "v": value, "t": source time }; each key keeps its own timestamp.</summary>
        public string Data { get; set; } = "{}";

        public DateTime LatestOnMapUpdate { get; set; }
    }
}
