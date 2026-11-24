namespace db_model.Companies
{
    public class UserJoinInvitationType
    {
        /// <summary>The company's anonymous join code (Email null): anyone who has it can ask to join.</summary>
        public const short Company = 1;
        /// <summary>An invitation to one email address; it works with the company's code, so its own JoinCode is null.</summary>
        public const short User = 2;
        /// <summary>An invitation to one email address with a code of its own, used once.</summary>
        public const short Personal = 3;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
