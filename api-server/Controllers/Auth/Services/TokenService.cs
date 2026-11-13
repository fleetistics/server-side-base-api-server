using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace api_server.Auth
{
    public class TokenService(IOptions<JwtOptions> jwtOptions)
    {
        private const string TokenUseClaim = "token_use";
        private const string AccessTokenUse = "access";

        // Not every session is tied to a physical device, so this claim is only
        // present when mobileGpsDeviceId is supplied.
        public const string MobileGpsDeviceIdClaim = "mobile_gps_device_id";

        // The user's active team at the time the token was issued. Only present
        // when the user has one; it is refreshed whenever the access token is renewed.
        public const string TeamIdClaim = "team_id";
        public const string CompanyIdClaim = "company_id";

        private readonly JwtOptions _options = jwtOptions.Value;

        // Secret behind the refresh-cookie MACs, derived from the signing key (so no extra deployment
        // setting). Its derivation label keeps it distinct from the key the access tokens are signed with.
        private readonly byte[] _refreshKey = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey),
            Encoding.UTF8.GetBytes("refresh-rotation-key-v1"));

        public string CreateAccessToken(string userId, string sessionId, string? mobileGpsDeviceId = null, IEnumerable<string>? roles = null, string? teamId = null, string? companyId = null)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId),
                //new(JwtRegisteredClaimNames.UniqueName, userName),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Sid, sessionId),
                new(TokenUseClaim, AccessTokenUse),
            };
            foreach (var role in roles ?? [])
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
            if (!string.IsNullOrEmpty(mobileGpsDeviceId))
            {
                claims.Add(new Claim(MobileGpsDeviceIdClaim, mobileGpsDeviceId));
            }
            if (!string.IsNullOrEmpty(teamId))
            {
                claims.Add(new Claim(TeamIdClaim, teamId));
            }
            if (!string.IsNullOrEmpty(companyId))
            {
                claims.Add(new Claim(CompanyIdClaim, companyId));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // 256 bits of entropy, URL-safe. Used for the per-session key salt and for password reset keys.
        public static string GenerateKey() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

        // The refresh cookie value: "{sessionId}.{counter}.{mac}". The mac is
        // HMAC-SHA256(server secret, "rt1|{sessionId}|{counter}|{salt}"), so the server can recompute the key of
        // any counter of a session (it never has to store one) while nobody without the secret can forge it.
        // Base64url never contains '.', so the split is unambiguous.
        public string CreateRotationKey(int sessionId, int counter, string salt) =>
            $"{sessionId}.{counter}.{Base64UrlEncoder.Encode(ComputeRotationMac(sessionId, counter, salt))}";

        /// <summary>Splits a cookie value into its parts; false for anything that is not three well-formed parts.</summary>
        public static bool TryParseRotationKey(string? value, out int sessionId, out int counter, out byte[] mac)
        {
            sessionId = 0;
            counter = 0;
            mac = [];
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            var parts = value.Split('.');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out sessionId) ||
                !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out counter))
            {
                return false;
            }
            try
            {
                mac = Base64UrlEncoder.DecodeBytes(parts[2]);
            }
            catch (FormatException)
            {
                return false;
            }
            return mac.Length == HMACSHA256.HashSizeInBytes;
        }

        /// <summary>Constant-time check of a presented mac against the one the session's salt yields.</summary>
        public bool IsValidRotationMac(int sessionId, int counter, string salt, byte[] presentedMac) =>
            CryptographicOperations.FixedTimeEquals(ComputeRotationMac(sessionId, counter, salt), presentedMac);

        private byte[] ComputeRotationMac(int sessionId, int counter, string salt) =>
            HMACSHA256.HashData(_refreshKey, Encoding.UTF8.GetBytes($"rt1|{sessionId}|{counter}|{salt}"));

        // Scoped to /api/auth only: the SPA never needs to send this cookie to
        // ordinary API endpoints, so narrowing the path shrinks the exposure
        // if any endpoint were ever vulnerable to XSS/CSRF.
        // persistentUntilUtc null -> session cookie (no Expires); the server-side session expiry
        // is what actually ends such a session.
        public CookieOptions BuildRefreshCookieOptions(bool isHttps, DateTime? persistentUntilUtc)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                // Always Secure unless a developer explicitly opted out (http dev / tests); the proxy-
                // reported scheme alone must not decide, or a misconfigured proxy would ship the cookie in clear.
                Secure = isHttps || !_options.AllowInsecureRefreshCookie,
                SameSite = SameSiteMode.Lax,
                Path = RefreshCookiePath,
                Expires = persistentUntilUtc is { } until
                    ? new DateTimeOffset(DateTime.SpecifyKind(until, DateTimeKind.Utc))
                    : null,
            };
        }

        public const string RefreshCookiePath = "/api/auth";

        public string RefreshCookieName => _options.RefreshCookieName;
        public bool AllowInsecureRefreshCookie => _options.AllowInsecureRefreshCookie;
        public int RefreshTokenDays => _options.RefreshTokenDays;
        public TimeSpan SessionOnlyIdleLifetime => TimeSpan.FromHours(_options.SessionOnlyIdleHours);
        public TimeSpan SessionOnlyMaxLifetime => TimeSpan.FromDays(_options.SessionOnlyMaxDays);
        public TimeSpan RefreshReuseGraceWindow => TimeSpan.FromSeconds(_options.RefreshReuseGraceSeconds);
    }
}
