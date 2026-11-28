namespace db_model.UserManagement
{ 

    public class UserStatus
    {
        public const short JustCreated = 0;
        public const short Active = 1;
        /// <summary>Switched off but can be resumed (a suspended contact, for example).</summary>
        public const short Suspended = 13;
		/// <summary>Terminal: an archived user or contact cannot be changed or brought back.</summary>
		public const short Archived = 14;

		public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
