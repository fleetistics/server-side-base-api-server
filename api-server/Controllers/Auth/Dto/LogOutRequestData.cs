using System.Text.Json.Serialization;

namespace api_server.Auth.Dto
{
    public class LogOutRequestData
    {
        public string SessionKey { get; set; } = string.Empty;


    }
}
