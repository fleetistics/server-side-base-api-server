namespace db_model.Contacts
{
    public class CompanyType
    {
        public const short Customer = 1;
        public const short Vendor = 2;
        public const short Partner = 3;
        public const short Affiliate = 4;
        public const short Internal = 5;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
