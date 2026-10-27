namespace db_model.UserManagement
{
    /// <summary>
    /// One outstanding password reset / activation key. A user can hold several at once
    /// (requesting a new key does not cancel the earlier ones); all of them are removed
    /// when the user sets a password.
    /// </summary>
    public class UserPasswordResetKey
    {
        /// <summary>
        /// Lower-case hex SHA-256 of the key. The key itself is only ever handed to the user,
        /// so a database leak does not expose usable keys. Random 256-bit keys cannot collide,
        /// which is what makes the hash usable as the primary key.
        /// </summary>
        public string KeyHash { get; set; } = default!;

        public int UserId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
