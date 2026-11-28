namespace db_model.Contacts
{
    /// <summary>
    /// A call, meeting, email or visit with a company or a person, logged after it happened (Completed) or scheduled for later (Scheduled).
    /// Belongs to the owner company. OwnerUserId null: every user of the owner company sees it; a user id: only that user does.
    /// The company is the account it is about (the owner's own company is allowed); the contact is the person, when there is one.
    /// </summary>
    public class Activity
    {
        public int Id { get; set; }
        public int OwnerCompanyId { get; set; }
        public int? OwnerUserId { get; set; }

        public short TypeId { get; set; }
        public string Subject { get; set; } = default!;
        public string? Notes { get; set; }
        public short PriorityId { get; set; } = ActivityPriority.Normal;
        public short StatusId { get; set; } = ActivityStatus.Scheduled;

        /// <summary>The account it is about; null when it is about a person who has no company.</summary>
        public int? CompanyId { get; set; }
        public int? ContactUserId { get; set; }
        public int? AssignedToUserId { get; set; }
        public int? CreatedByUserId { get; set; }

        public DateTime StartAt { get; set; }
        public int DurationMinutes { get; set; }
        public DateTime? CompletedAt { get; set; }

        /// <summary>Set when the activity is one occurrence of a recurring one.</summary>
        public int? SeriesId { get; set; }

        public DateTime CreationDate { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
