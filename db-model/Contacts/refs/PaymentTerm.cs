namespace db_model.Contacts
{
    public class PaymentTerm
    {
        public const short DueOnReceipt = 1;
        public const short Net15 = 2;
        public const short Net30 = 3;
        public const short Net45 = 4;
        public const short Net60 = 5;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
