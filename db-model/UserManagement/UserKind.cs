namespace db_model.UserManagement
{
    /// <summary>What a User row is (User.KindId), not how it was made (that is <see cref="UserSource"/>). Constants only, no table.</summary>
    public static class UserKind
    {
        /// <summary>A person who works with the platform (a colleague, a customer user).</summary>
        public const short User = 1;
        /// <summary>A contact person kept in a company's Contacts; never signs in.</summary>
        public const short Contact = 2;
        /// <summary>A user made for tests or simulation (device test users, fake users, mock team members).</summary>
        public const short Test = 3;
    }
}
