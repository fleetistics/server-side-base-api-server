using System;
using System.Collections.Generic;
using System.Text;
using NetTopologySuite.Geometries;

namespace db_model.Map
{
    public class MobileGpsDeviceMapState
    {
        public int MobileGpsDeviceId { get; set; }
        public int UserId { get; set; }

        public Point Location { get; set; } = default!;
        public short? MotionActivity { get; set; }
        public float? Speed { get; set; }
        public short? Dir { get; set; }
        public float? BatteryLevel { get; set; }


        public DateTime LatestOnMapUpdate { get; set; }
    }
}
