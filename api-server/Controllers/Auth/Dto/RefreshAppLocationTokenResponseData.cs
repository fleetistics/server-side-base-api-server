namespace api_server.Controllers.Auth.Dto
{
    public class RefreshAppLocationTokenResponseData
    {
        public string access_token { get; set; } = string.Empty;
        public string refresh_token { get; set; } = string.Empty;
        public int expires_in { get; set; } = 0;
    }
}
