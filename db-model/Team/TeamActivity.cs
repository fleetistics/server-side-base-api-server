using db_model.Messages;
using NetTopologySuite.Geometries;

namespace db_model.Team
{
    public class TeamActivity
    {
        public int Id { get; set; }
        public int TeamId { get; set; }
        public DateTime Date { get; set; }
        public int? UserId { get; set; }
        public short ActivityTypeId { get; set; }
        public Point? Location { get; set; } = default!;
        public string? Message { get; set; } = default!;

        public List<TeamActivity2User> TeamActivity2Users { get; set; } = default!;

    }
}
