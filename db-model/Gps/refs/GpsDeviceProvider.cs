namespace db_model.Gps
{ 

    public class GpsDeviceProvider
    {
        public const short Unknown = 0;
		public const short Geotab = 3;
		/// <summary>Phones running our app. Ids 1 and 2 were the old Ios/Android providers: never reuse them.</summary>
		public const short Mobile = 4;

		public short Id { get; set; }
        public string Name { get; set; } = default!;

        public DateTime LatestUpdate { get; set; }
    }
}
