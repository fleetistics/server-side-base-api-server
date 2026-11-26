namespace db_model.Contacts
{
    public class LeadSource
    {
        public const short Website = 1;
        public const short Referral = 2;
        public const short TradeShow = 3;
        public const short ColdCall = 4;
        public const short Partner = 5;
        public const short Advertising = 6;
        public const short Other = 7;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
