using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace api_server.Auth.Dto
{
    public class ClientSideInfo
    {
        public short AppId { get; set; } = 0;
        public short PlatformId { get; set; } = 0;


        [MaxLength(10)]
        public string AppVersion { get; set; } = string.Empty;
        
        [MaxLength(50)]
        public string AppHash { get; set; } = string.Empty;

        [MaxLength(8196)]
        public string PlatformDetails { get; set; } = string.Empty;


        [MaxLength(256)]
        public string? FCM_FID { get; set; }
    }
}
