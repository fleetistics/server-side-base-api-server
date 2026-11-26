namespace db_model.Contacts
{
    /// <summary>
    /// A person who is a contact of the owner company. The person exists only for this owner.
    /// OwnerUserId null: every user of the owner company sees it; a user id: only that user does.
    /// </summary>
    public class CompanyUserContact
    {
        public int OwnerCompanyId { get; set; }
        public int ContactUserId { get; set; }
        public int? OwnerUserId { get; set; }

        public bool IsArchived { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
