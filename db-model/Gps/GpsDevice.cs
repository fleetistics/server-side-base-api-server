using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Gps
{
    public class GpsDevice
    {
        public int Id { get; set; }
        public int ProviderId { get; set; }
        /// <summary><see cref="GpsDeviceType"/> id (a leaf of the type tree); null when unknown.</summary>
        public int? TypeId { get; set; }
        /// <summary><see cref="GpsAssetType"/> id: what the device is on (car, truck, phone ...); null when unknown.</summary>
        public int? AssetTypeId { get; set; }

        public short StatusId { get; set; }

        public string SerialNumber { get; set; }

        public DateTime LatestUpdate { get; set; }

        public int? UserId { get; set; }
        public int? CompanyId { get; set; }
        
        public string? Name { get; set; }

    }
}
