using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.UserManagement
{
    public class UserEmergencyAlert
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public float? Latitude { get; set; }
        public float? Longitude { get; set; }
        public float? CompleteLatitude { get; set; }
        public float? CompleteLongitude { get; set; }
        public DateTime LatestUpdate { get; set; }
        public short StatusId { get; set; }
    }
}
