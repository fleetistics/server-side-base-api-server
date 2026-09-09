using db_model.Media;

namespace db_model.Team
{
    public class TeamUploadedMedia
    {
        public int TeamId { get; set; }
        public int UploadedMediaId { get; set; }
        public short? GroupKey { get; set; }
        public DateTime LatestUpdate { get; set; }

        public UploadedMedia? Media { get; set; }

    }
}
