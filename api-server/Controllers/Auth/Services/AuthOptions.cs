namespace api_server.Auth
{
    /// <summary>How password-reset requests identify a user.</summary>
    public enum EmailUsageMode
    {
        /// <summary>Only the user name is guaranteed unique; reset requests look the user up by user name.</summary>
        UniqueUserName,

        /// <summary>E-mail is unique across the system; reset requests look the user up by e-mail.</summary>
        UniqueEmail,
    }

    /// <summary>Bound from the "Auth" configuration section.</summary>
    public class AuthOptions
    {
        public int PasswordResetKeyMinutes { get; set; } = 60;

        /// <summary>
        /// Lifetime of the key e-mailed on registration. Longer than a reset key: a new user may
        /// not open the message for a while, and the key is their only way to activate the account.
        /// </summary>
        public int RegistrationKeyMinutes { get; set; } = 24 * 60;

        /// <summary>
        /// After a reset key is issued, further reset requests for the same user are ignored for
        /// this long. Keys no longer replace each other, so this only caps how many e-mails one
        /// account can be sent and how many keys it can accumulate. 0 disables it.
        /// </summary>
        public int PasswordResetCooldownSeconds { get; set; } = 300;

        /// <summary>
        /// Least time a password reset request takes to answer, whatever it did. Hides whether the
        /// identifier matched a user (a match writes a key and sends a message, a miss does not).
        /// Must exceed the slowest real run to be effective. 0 disables it.
        /// </summary>
        public int PasswordResetMinResponseMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Failed logins for one account (counted per user name, whether or not it exists) within
        /// <see cref="LoginFailureWindowMinutes"/> that lock it for <see cref="LoginLockoutMinutes"/>.
        /// While locked, even the right password is refused. 0 disables the throttle.
        /// </summary>
        public int LoginMaxFailedAttempts { get; set; } = 5;
        public int LoginFailureWindowMinutes { get; set; } = 15;
        public int LoginLockoutMinutes { get; set; } = 15;

        public TimeSpan LoginFailureWindow => TimeSpan.FromMinutes(Math.Max(1, LoginFailureWindowMinutes));
        public TimeSpan LoginLockout => TimeSpan.FromMinutes(Math.Max(1, LoginLockoutMinutes));

        public EmailUsageMode EmailMode { get; set; } = EmailUsageMode.UniqueUserName;

        public TimeSpan PasswordResetKeyLifetime =>
            TimeSpan.FromMinutes(PasswordResetKeyMinutes > 0 ? PasswordResetKeyMinutes : 60);

        public TimeSpan RegistrationKeyLifetime =>
            TimeSpan.FromMinutes(RegistrationKeyMinutes > 0 ? RegistrationKeyMinutes : 24 * 60);

        public TimeSpan PasswordResetMinResponseTime =>
            TimeSpan.FromMilliseconds(Math.Max(0, PasswordResetMinResponseMilliseconds));

        public TimeSpan PasswordResetCooldown =>
            TimeSpan.FromSeconds(Math.Max(0, PasswordResetCooldownSeconds));
    }
}
