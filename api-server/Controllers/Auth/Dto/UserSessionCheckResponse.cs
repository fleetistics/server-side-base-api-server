using System.Text.Json.Serialization;

namespace api_server.Auth.Dto
{
    public class UserSessionCheckResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        public int UserId { get; set; }

        public int SessionId { get; set; }
    }
}
