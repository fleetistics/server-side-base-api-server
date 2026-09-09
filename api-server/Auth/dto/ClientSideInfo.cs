using System.Text.Json.Serialization;
using db_model.AppStructure;

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
        public string FCMToken { get; set; } = string.Empty;

    }
}
