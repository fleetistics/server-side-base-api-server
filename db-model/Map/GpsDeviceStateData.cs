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

        /// <summary>Device time of the newest acceptable position; null until the device reported one. Only moves forward.</summary>
        public DateTime? LastLocationDeviceDate { get; set; }

        /// <summary>
        /// Server time at which <see cref="LastLocationDeviceDate"/> was last changed. Set by the database only: on insert when
        /// <see cref="LastLocationDeviceDate"/> is not null, and on every update that changes it. Never assign it.
        /// </summary>
        public DateTime? LastLocationServerDate { get; set; }

        /// <summary>Set by the database (DEFAULT now() and the update trigger); not an optimistic-lock token for this table.</summary>
        public DateTime LatestUpdate { get; set; }
    }
}
