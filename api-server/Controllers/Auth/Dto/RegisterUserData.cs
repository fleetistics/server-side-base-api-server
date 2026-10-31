namespace api_server.Auth.Dto
{
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

        public ClientSideInfo? ClientSideInfo { get; set; }
    }
}
