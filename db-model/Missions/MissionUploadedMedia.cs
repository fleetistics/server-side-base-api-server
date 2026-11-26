using db_model.Media;

namespace db_model.Missions
{
    public class MissionUploadedMedia
    {
        public int MissionId { get; set; }
        public int UploadedMediaId { get; set; }
        public short? GroupKey { get; set; } //-1 value means removed uploaded media
        public DateTime LatestUpdate { get; set; }

        public UploadedMedia? Media { get; set; }

    }
}
