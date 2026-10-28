using System;
using System.Collections.Generic;
using System.Text;
using NetTopologySuite.Geometries;

namespace db_model.Map
{
    /// <summary>
    /// Obsolete: phones' current state is now <see cref="GpsDeviceMapState"/> / <see cref="GpsDeviceStateData"/>
    /// (the rows here were not copied, phones refill the new tables as they report). Nothing reads or writes this
    /// any more; the entity and table stay only until the table is dropped by hand, so no migration is needed yet.
    /// </summary>
    [Obsolete("Phones use GpsDeviceMapState; kept only until mobile_gps_device_map_state is dropped.")]
    public class MobileGpsDeviceMapState
    {
        public int MobileGpsDeviceId { get; set; }
        public int UserId { get; set; }
        public int? TeamId { get; set; }

        public Point Location { get; set; } = default!;
        public short? MotionActivity { get; set; }
        public float? Speed { get; set; }
        public short? Dir { get; set; }
        public float? BatteryLevel { get; set; }


        public DateTime LatestOnMapUpdate { get; set; }
    }
}
