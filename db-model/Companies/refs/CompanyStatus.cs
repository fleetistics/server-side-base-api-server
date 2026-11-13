namespace db_model.Companies
{ 

    public class CompanyStatus
    {
        public const short Active = 1;
        public const short Inactive = 13;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
