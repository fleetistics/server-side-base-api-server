namespace db_model.Companies
{ 

    public class CompanyKind
    {
        public const short Client = 1;
        public const short ContactsEntry = 2;
        public const short SingleMissionHolder = 3;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
