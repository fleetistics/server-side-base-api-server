namespace db_model.Contacts
{
    /// <summary>
    /// How a recurring activity repeats. Every occurrence is its own <see cref="Activity"/> row that points here (the rows are made when the activity is
    /// scheduled); this row only keeps the rule that made them. The series ends after <see cref="EndAfterCount"/> occurrences or on <see cref="EndOnDate"/>.
    /// </summary>
    public class ActivitySeries
    {
        public int Id { get; set; }
        public int OwnerCompanyId { get; set; }
        public short FrequencyId { get; set; }
        /// <summary>Repeat every this many days / weeks / months / years.</summary>
        public int Interval { get; set; } = 1;
        public int? EndAfterCount { get; set; }
        public DateOnly? EndOnDate { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
