using System.ComponentModel.DataAnnotations;

namespace api_server.Auth.Dto
{
    public class LoginData
    {
        [Required]
        public string UserName { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
        public bool? RememberMe { get; set; }
    }
}
