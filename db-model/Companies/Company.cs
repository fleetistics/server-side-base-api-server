namespace db_model.Companies
{
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
