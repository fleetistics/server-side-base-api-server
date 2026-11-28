namespace db_model.Contacts
{
    /// <summary>What kind of employee a contact is (UserInfo.EmployeeStatusId); only used when the contact type is Employee.</summary>
    public class EmployeeStatus
    {
        public const short Office = 1;
        public const short Manager = 2;
        public const short Driver = 3;
        public const short Warehouse = 4;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
