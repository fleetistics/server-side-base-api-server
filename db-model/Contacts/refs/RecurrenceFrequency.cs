namespace db_model.Contacts
{
    public class RecurrenceFrequency
    {
        public const short Daily = 1;
        public const short Weekly = 2;
        public const short Monthly = 3;
        public const short Yearly = 4;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
