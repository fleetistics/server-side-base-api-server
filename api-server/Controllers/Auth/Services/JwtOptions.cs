namespace api_server.Auth
{
    public class JwtOptions
    {
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string SigningKey { get; set; } = string.Empty;
        public int AccessTokenMinutes { get; set; } = 15;
        public int RefreshTokenDays { get; set; } = 14;
        public string RefreshCookieName { get; set; } = "mf_refresh_token";

        // Logins without "remember me": the session ends after this many hours without a refresh
        // (each refresh pushes it forward), but never later than SessionOnlyMaxDays after login.
        public int SessionOnlyIdleHours { get; set; } = 12;
        public int SessionOnlyMaxDays { get; set; } = 7;

        // How long a just-superseded rotation key still works. Covers benign races
        // (two tabs, an app relaunch, a retried request) without weakening detection
        // of a genuinely stale/stolen key replayed well after the legitimate holder moved on.
        public int RefreshReuseGraceSeconds { get; set; } = 10;
    }
}
