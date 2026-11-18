using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace api_server.Auth
{
    /// <summary>A refresh key in both forms: its three fields, and the {SessionKey}.{RotationCounter}.{SessionSign} string for the cookie.</summary>
    public sealed record RotationKey(string SessionKey, int RotationCounter, string SessionSign, string CompositeCookieKey);

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

        // The refresh key: "{sessionKey}.{counter}.{mac}". sessionKey is the session's random handle
        // (UserSession.SessionKey, see GenerateKey), not its database id. The mac is
        // HMAC-SHA256(server secret, "rt2|{sessionKey}|{counter}|{salt}"), so the server can recompute the key of
        // any counter of a session (it never has to store one) while nobody without the secret can forge it.
        // Base64url never contains '.', so the split is unambiguous.
        // The mac is computed once and handed out both as the separate fields and as the combined value, so nobody
        // downstream has to sign, split or join anything.
        public RotationKey CreateRotationKey(string sessionKey, int counter, string salt)
        {
            var sign = Base64UrlEncoder.Encode(ComputeRotationMac(sessionKey, counter, salt));
            return new RotationKey(sessionKey, counter, sign, $"{sessionKey}.{counter}.{sign}");
        }

        // A SessionKey is GenerateKey() output: 32 random bytes in base64url (43 characters, no padding).
        private const int SessionKeyLength = 43;

        /// <summary>Splits a key into its parts; null for anything that is not three well-formed parts.
        /// Only the shape is checked here (the mac is not verified): that needs the session's salt, see <see cref="IsValidRotationMac"/>.</summary>
        public static RotationKey? TryParseRotationKey(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            var parts = value.Split('.');
            if (parts.Length != 3 ||
                parts[0].Length != SessionKeyLength ||
                !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var counter))
            {
                return null;
            }
            try
            {
                if (Base64UrlEncoder.DecodeBytes(parts[2]).Length != HMACSHA256.HashSizeInBytes)
                {
                    return null;
                }
            }
            catch (FormatException)
            {
                return null;
            }
            return new RotationKey(parts[0], counter, parts[2], value);
        }

        /// <summary>Constant-time check of a presented mac against the one the session's salt yields.</summary>
        //public bool IsValidRotationMac(string sessionKey, int counter, string salt, byte[] presentedMac) =>
        //    CryptographicOperations.FixedTimeEquals(ComputeRotationMac(sessionKey, counter, salt), presentedMac);

        /// <summary>Same check for a mac that arrives as text; a malformed one is simply not valid (never an exception).</summary>
        public bool IsValidRotationMac(string presentedSessionKey, int presentedCounter, string salt, string? presentedMac)
        {
            if (string.IsNullOrEmpty(presentedMac))
            {
                return false;
            }
            byte[] mac;
            try
            {
                mac = Base64UrlEncoder.DecodeBytes(presentedMac);
            }
            catch (FormatException)
            {
                return false;
            }
            return CryptographicOperations.FixedTimeEquals(ComputeRotationMac(presentedSessionKey, presentedCounter, salt), mac);
        }

        private byte[] ComputeRotationMac(string sessionKey, int counter, string salt) =>
            HMACSHA256.HashData(_refreshKey, Encoding.UTF8.GetBytes($"rt2|{sessionKey}|{counter}|{salt}"));

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

        public const string RefreshCookiePath = "/api/auth_cookie";

        public string RefreshCookieName => _options.RefreshCookieName;
        public bool AllowInsecureRefreshCookie => _options.AllowInsecureRefreshCookie;
        public int RefreshTokenDays => _options.RefreshTokenDays;
        public TimeSpan SessionOnlyIdleLifetime => TimeSpan.FromHours(_options.SessionOnlyIdleHours);
        public TimeSpan SessionOnlyMaxLifetime => TimeSpan.FromDays(_options.SessionOnlyMaxDays);
        public TimeSpan RefreshReuseGraceWindow => TimeSpan.FromSeconds(_options.RefreshReuseGraceSeconds);
    }
}
