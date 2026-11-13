namespace db_model.Companies
{ 

    public class User2CompanyJoinType
    {
        public const short Manual = 1;
        public const short Invited = 2;
        public const short ExternalSync = 3;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
