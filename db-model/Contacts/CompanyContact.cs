namespace db_model.Contacts
{
    /// <summary>
    /// A company that is a contact of the owner company. The contact company exists only for this owner (no sharing between owners).
    /// OwnerUserId null: every user of the owner company sees it; a user id: only that user does.
    /// </summary>
    public class CompanyContact
    {
        public int OwnerCompanyId { get; set; }
        public int ContactCompanyId { get; set; }
        public int? OwnerUserId { get; set; }

        public DateTime CreationDate { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
