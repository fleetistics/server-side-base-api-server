using System.ComponentModel.DataAnnotations;

namespace api_server.Auth.Dto
{
    public class LoginRequestData
    {
        // Same cap as the throttle's key length; longer input is refused with a 400 before any lookup.
        [Required]
        [MaxLength(MaxUserNameLength)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
        public bool? RememberMe { get; set; }

        public ClientSideInfo ClientSideInfo { get; set; }

        // An identifier, so an over-long value is refused (400) rather than cut or ignored.
        [MaxLength(MaxGpsDeviceSerialLength)]
        public string? GpsDeviceSerial { get; set; }

        public const int MaxUserNameLength = 256;
        public const int MaxGpsDeviceSerialLength = 128;
    }
}
