using System.Text.Json.Serialization;

namespace api_server.Auth.Dto
{
    public class ClientSideInfo
    {

        public string AppUID { get; set; } = string.Empty;

        public string AppVersion { get; set; } = string.Empty;

        public string DeviceUID { get; set; } = string.Empty;
        public string CodeVersion { get; set; } = string.Empty;
        public string PlatformName { get; set; } = string.Empty;
        public short PlatformId { get; set; } = 0;
        public string FCM_FID { get; set; } = string.Empty;

    }
}
