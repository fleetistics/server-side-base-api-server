namespace api_server.Controllers.Auth.Dto
{
    public class RefreshKeyResponseData
    {
        public string? SessionSign { get; set; }
        public int? RotationCounter { get; set; }
        public string AccessToken { get; set; } = string.Empty;
    }
}
