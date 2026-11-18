using System.Text.Json.Serialization;

namespace api_server.Auth.Dto
{
    public class CheckSessionRequestData
    {
        public string? SessionSign { get; set; }
        public int? RotationCounter { get; set; }

        public string? SessionKey { get; set; }

        

        public ClientSideInfo ClientSideInfo { get; set; }

    }
}
