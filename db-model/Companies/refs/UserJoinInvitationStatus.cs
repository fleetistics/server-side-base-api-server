namespace db_model.Companies
{ 

    public class UserJoinInvitationStatus
    {
        public const short Active = 1;
        public const short Expired = 2;
        public const short Accepted = 3;
        public const short Cancelled = 4;
        /// <summary>The company join code is switched off by an admin (Company type only).</summary>
        public const short Disabled = 5;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
