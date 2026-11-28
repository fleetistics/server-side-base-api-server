namespace db_model.Contacts
{
    /// <summary>
    /// The CRM facts about a contact person. The person is a user row (kind Contact; their status Active, Suspended or Archived is the user status, and they have no password, so they are not an
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
        /// <summary>An EmployeeStatus id (Office, Manager, Driver, Warehouse); set only while the ContactTypeId is Employee.</summary>
        public short? EmployeeStatusId { get; set; }

        public DateTime LatestUpdate { get; set; }
    }
}
