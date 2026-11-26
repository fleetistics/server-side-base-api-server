namespace db_model.Contacts
{
    /// <summary>
    /// The CRM facts about a contact person. The person is a user row (source Contact, status Inactive, so nothing treats them as an
    /// account) whose email and direct phone are the user's own Email and Phone. One optional row per user, made on the first save.
    /// </summary>
    public class UserInfo
    {
        public int UserId { get; set; }

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Title { get; set; }
        public string? Mobile { get; set; }
        /// <summary>A ContactType id.</summary>
        public short? ContactTypeId { get; set; }
        public short? LeadSourceId { get; set; }
        /// <summary>The contact's own Active / Inactive state (the user row is always Inactive).</summary>
        public bool IsActive { get; set; } = true;

        public DateTime LatestUpdate { get; set; }
    }
}
