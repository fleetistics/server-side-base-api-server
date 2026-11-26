namespace db_model.Contacts
{
    /// <summary>
    /// The CRM facts about a company (its own company, or a company that is somebody's contact): one optional row per company,
    /// made on the first save. Every field can be empty. Read it only through the owner's link (CompanyContact) or for the caller's own company.
    /// </summary>
    public class CompanyInfo
    {
        public int CompanyId { get; set; }

        /// <summary>A CompanyType id. Null for the caller's own company, which is shown as Internal.</summary>
        public short? TypeId { get; set; }
        public string? LegalName { get; set; }
        public string? Industry { get; set; }
        public string? Website { get; set; }
        public string? MainPhone { get; set; }
        /// <summary>Shown as ORG-00001; made from the company id when the row is first saved.</summary>
        public string? AccountNumber { get; set; }
        /// <summary>A user of the owner company.</summary>
        public int? AccountManagerUserId { get; set; }
        public short? PaymentTermId { get; set; }
        public string? TaxId { get; set; }
        public int? Employees { get; set; }
        public int? FleetVehicles { get; set; }
        public DateOnly? CustomerSince { get; set; }
        public decimal? AnnualRevenue { get; set; }
        public string? Territory { get; set; }
        public short? LeadSourceId { get; set; }
        /// <summary>The company's primary contact (a contact user whose CompanyId is this company).</summary>
        public int? PrimaryContactUserId { get; set; }

        public DateTime LatestUpdate { get; set; }
    }
}
