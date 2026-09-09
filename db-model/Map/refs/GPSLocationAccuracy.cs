namespace db_model.Map
{

    public class GPSLocationAccuracy
    {
        public const byte Fine = 1;
        public const byte Fair = 2;
        public const byte Poor = 3;
        public const byte Bad = 4;

        public byte Id { get; set; }
        public string Name { get; set; } = default!;

        public DateTime LatestUpdate { get; set; }
    }
}
