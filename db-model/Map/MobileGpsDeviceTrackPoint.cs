using System;
using System.Collections.Generic;
using System.Text;
using NetTopologySuite.Geometries;

namespace db_model.Map
{
    public class MobileGpsDeviceTrackPoint
    {
        public int Id { get; set; }
        public int GpsDeviceId { get; set; }
        public int? CompanyId { get; set; }
        public DateTime DeviceDate { get; set; }
        public Point Location { get; set; } = default!;
        public float? Speed { get; set; }
        public short? Dir { get; set; }
        public short LocationAccuracy { get; set; }

        /// <summary><see cref="db_model.Map.GpsDeviceActivityStatus"/> value computed for this fix (moving, idle, parked); the phone's own motion activity is in <see cref="Data"/>.</summary>
        public short? ActivityStatusId { get; set; }

        /// <summary>The accepted values of this fix as a <see cref="DeviceStateSnapshot"/> (JSON, plain values without dates).</summary>
        public string Data { get; set; } = "{}";

        /// <summary>The location item exactly as the phone sent it (JSON, nothing filtered), kept so the ignored values are not lost.</summary>
        public string RawData { get; set; } = "{}";

        /// <summary>Set by the database (DEFAULT now() and the update trigger).</summary>
        public DateTime LatestUpdate { get; set; }

    }
}
