namespace db_model.Companies
{ 

    public class User2CompanyStatus
    {
        
        public const short Active = 1;
        public const short Suspended = 2;
        public const short Deleted = 3;


        //still active for this company but for now switched by user to the other company
        //company admin can suspend
        public const short WithOtherCompany = 4;

        //left by user, company Admin can suspend
        public const short Left = 5;

        //Suspended by Site Admin, it is done with Suspend for User. not editable
        public const short SuspendedBySiteAdmin = 13;
        public const short DeletedBySiteAdmin = 14;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
