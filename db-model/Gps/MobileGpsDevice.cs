using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Gps
{
    public class MobileGpsDevice
    {
        public int Id { get; set; }
        public int ProviderId { get; set; }

        public short StatusId { get; set; }

        public string DeviceUID { get; set; }

        public DateTime LatestUpdate { get; set; }

        public int? UserId { get; set; }

        public string? Name { get; set; }
    }
}
