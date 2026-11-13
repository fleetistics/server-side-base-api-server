namespace db_model.Companies
{ 

    public class User2CompanyStatus
    {
        public const short Creating = 1;
        public const short Active = 2;
        public const short WithOtherCompany = 3;
        public const short Left = 4;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
