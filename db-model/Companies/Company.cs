namespace db_model.Companies
{
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;

        public short StatusId { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
