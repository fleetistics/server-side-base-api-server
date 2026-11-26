namespace db_model.Contacts
{
    /// <summary>An address of a company: a job site, the billing address, or one place that is both ("same as job address").</summary>
    public class CompanyLocation
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }

        public string? Name { get; set; }
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }

        public bool IsJobSite { get; set; }
        public bool IsBilling { get; set; }

        public DateTime LatestUpdate { get; set; }
    }
}
