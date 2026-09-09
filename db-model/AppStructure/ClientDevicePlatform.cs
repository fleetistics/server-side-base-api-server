namespace db_model.AppStructure
{
    public class ClientDevicePlatform
    {
        public const short Unknown = 0;
        public const short Ios = 1;
        public const short Android = 2;
        public const short Windows = 3;

        public short Id { get; set; }
        public string Name { get; set; }
        public string PlatformKeys { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
