using NetTopologySuite.Geometries;

namespace db_model.Map
{
    /// <summary>
    /// Current map state of a GPS device (vehicle/tracker), one row per device. Written by the feeders,
    /// read by the API. Kept narrow on purpose: it changes every few seconds, so slow-changing data lives
    /// in <see cref="GpsDeviceStateData"/> and no indexed column changes on a position update.
    /// </summary>
    public class GpsDeviceMapState
    {
        public int GpsDeviceId { get; set; }
        /// <summary>Null for a device with no company (a phone of a user outside any company).</summary>
        public int? CompanyId { get; set; }

        public Point Location { get; set; } = default!;
        public float? Speed { get; set; }
        /// <summary>Heading in degrees, null when unknown.</summary>
        public short? Dir { get; set; }

        /// <summary><see cref="db_model.Map.GpsDeviceMapStatus"/> value: whether the map shows the device.</summary>
        public short StatusId { get; set; } = GpsDeviceMapStatus.Visible;
        /// <summary><see cref="db_model.Map.GpsDeviceActivityStatus"/> value: moving, idle, parked ...</summary>
        public short ActivityStatusId { get; set; }
        public GpsDeviceWarning WarningFlags { get; set; }
        /// <summary><see cref="db_model.Map.GPSLocationAccuracy"/> value of the newest position.</summary>
        public short LocationAccuracyId { get; set; } = GPSLocationAccuracy.Fine;

        /// <summary>Source time of the newest position; an upsert only applies a newer one.</summary>
        public DateTime LastPositionAt { get; set; }

        public DateTime LatestOnMapUpdate { get; set; }
    }
}
