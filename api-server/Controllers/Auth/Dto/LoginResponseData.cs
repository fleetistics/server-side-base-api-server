using api_server.Controllers.Auth.Dto;

namespace api_server.Auth.Dto
{
    public class LoginResponseData
    {
        public string? SessionSign { get; set; }
        public int? RotationCounter { get; set; }
        public string? SessionKey { get; set; }

        public string AccessToken { get; set; } = string.Empty;

        public UserSessionInfoDto UserSessionInfo { get; set; }
    }
}
