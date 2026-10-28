namespace api_server.Auth.Dto
{
    public class RequestPasswordResetData
    {
        /// <summary>
        /// The e-mail address when Auth:EmailMode is UniqueEmail, the user name otherwise.
        /// Optional on the wire: a missing or unknown value is silently ignored.
        /// </summary>
        public string? Identifier { get; set; }
    }

    /// <summary>
    /// Self-registration. UserName is required only when Auth:EmailMode is not UniqueEmail;
    /// in UniqueEmail mode it is ignored and the e-mail becomes the user name.
    /// </summary>
    public class RegisterUserData
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
    }

    public class ResetPasswordData
    {
        public string ResetKey { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
