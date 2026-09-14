namespace db_model.Media
{ 

    public class MediaStatus
    {
        public const short JustCreated = 0;
        public const short Uploaded = 1;
        public const short Removed = 13;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
