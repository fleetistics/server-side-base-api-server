namespace db_model.Contacts
{
    public class ActivityType
    {
        public const short CallComplete = 1;
        public const short CallAttempt = 2;
        public const short CallVoicemail = 3;
        public const short Meeting = 4;
        public const short Email = 5;
        public const short SiteVisit = 6;
        public const short Webinar = 7;
        public const short Other = 8;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
