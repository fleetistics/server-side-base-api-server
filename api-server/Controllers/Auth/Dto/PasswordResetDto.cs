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

    

    public class ResetPasswordData
    {
        public string ResetKey { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
