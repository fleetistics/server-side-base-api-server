using System.Text.Json.Serialization;

namespace api_server.Auth.Dto
{
    public class RefreshKeyRequestData
    {
        public string SessionSign { get; set; } = string.Empty;
        public int RotationCounter { get; set; } = 0;

        public string SessionKey { get; set; } = string.Empty;


    }
}
