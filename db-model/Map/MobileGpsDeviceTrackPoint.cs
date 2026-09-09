using System;
using System.Collections.Generic;
using System.Text;
using NetTopologySuite.Geometries;

namespace db_model.Map
{
    public class MobileGpsDeviceTrackPoint
    {
        public int Id { get; set; }
        public int MobileGpsDeviceId { get; set; }
        public DateTime ReceivedDate { get; set; }
        public DateTime DeviceDate { get; set; }
        public Point Location { get; set; } = default!;
        public float? Speed { get; set; }
        public float? Odometer { get; set; }
        public short? Dir { get; set; }
        public short LocationAccuracy { get; set; }
        public float? BatteryLevel { get; set; }
        public bool? BatteryIsCharging { get; set; }
        public short? MotionActivity { get; set; }
    }
}
