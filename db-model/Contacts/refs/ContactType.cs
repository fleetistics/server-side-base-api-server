namespace db_model.Contacts
{
    public class ContactType
    {
        public const short Contact = 1;
        public const short Customer = 2;
        public const short Vendor = 3;
		public const short Employee = 4;

		public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
