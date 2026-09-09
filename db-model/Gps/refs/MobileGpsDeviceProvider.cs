namespace db_model.Gps
{ 

    public class MobileGpsDeviceProvider
    {
        public const short Unknown = 0;
        public const short Ios = 1;
        public const short Android = 2;

        public short Id { get; set; }
        public string Name { get; set; } = default!;

        public DateTime LatestUpdate { get; set; }
    }
}
