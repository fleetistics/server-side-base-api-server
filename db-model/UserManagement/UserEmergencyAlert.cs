using NetTopologySuite.Geometries;

namespace db_model.UserManagement
{
    public class UserEmergencyAlert
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int? CompanyId { get; set; }
        public int? GpsDeviceId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public Point? Location { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
        public short StatusId { get; set; }
    }
}
