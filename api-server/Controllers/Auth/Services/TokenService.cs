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

    /// <summary>Which refresh chain of a session a rotation key belongs to. Each chain has its own counter
    /// (UserSession.Token.RotationCounter / LocationReportTokenRotationCounter) and its own MAC domain, so a key
    /// of one chain never verifies as a key of the other, even when both counters hold the same value.</summary>
    public enum RotationKeyPurpose
    {
        /// <summary>The main session chain: refresh cookie, /api/auth/refresh, check-session.</summary>
        Session,
        /// <summary>The app's location-report chain: /api/auth/refresh-app-location-token.</summary>
        LocationReport,
    }

    /// <summary>Which endpoints an access token is for. Each kind is issued with its own audience and accepted only
    /// by its own authentication scheme (see AuthSchemes), so a token of one kind is refused everywhere the other
    /// kind is expected.</summary>
    public enum AccessTokenKind
    {
        /// <summary>Every API endpoint except the location report (AuthAPIController, default scheme).</summary>
        Api,
        /// <summary>Only the location report (LocationAuthAPIController, AuthSchemes.LocationReport).</summary>
        LocationReport,
    }

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

        public string CreateAccessToken(string userId, string sessionId, string? mobileGpsDeviceId = null, IEnumerable<string>? roles = null, string? teamId = null, string? companyId = null, AccessTokenKind kind = AccessTokenKind.Api)
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
                audience: kind switch
                {
                    AccessTokenKind.Api => _options.Audience,
                    AccessTokenKind.LocationReport => _options.LocationReportAudience,
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
                },
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // 256 bits of entropy, URL-safe. Used for the per-session key salt and for password reset keys.
        public static string GenerateKey() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

        // The refresh key: "{sessionKey}.{counter}.{mac}". sessionKey is the session's random handle
        // (UserSession.SessionKey, see GenerateKey), not its database id. The mac is
        // HMAC-SHA256(server secret, "{domain}|{sessionKey}|{counter}|{salt}"), so the server can recompute the key of
        // any counter of a session (it never has to store one) while nobody without the secret can forge it.
        // The domain label depends on the purpose (see MacDomain): both chains share sessionKey and salt, so without
        // it the location key for counter N would be the very same string as the session key for counter N.
        // Base64url never contains '.', so the split is unambiguous.
        // The mac is computed once and handed out both as the separate fields and as the combined value, so nobody
        // downstream has to sign, split or join anything.
        public RotationKey CreateRotationKey(string sessionKey, int counter, string salt, RotationKeyPurpose purpose)
        {
            var sign = Base64UrlEncoder.Encode(ComputeRotationMac(sessionKey, counter, salt, purpose));
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

        /// <summary>Same check for a mac that arrives as text; a malformed one is simply not valid (never an exception).
        /// A key of the other chain (<paramref name="purpose"/>) is not valid either.</summary>
        public bool IsValidRotationMac(string presentedSessionKey, int presentedCounter, string salt, string? presentedMac, RotationKeyPurpose purpose)
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
            return CryptographicOperations.FixedTimeEquals(ComputeRotationMac(presentedSessionKey, presentedCounter, salt, purpose), mac);
        }
        public int GetAccesstokenExpirationInSeconds() => _options.AccessTokenMinutes * 60;

        // Session keeps "rt2", the label every key issued so far was signed with, so existing cookies stay valid.
        private static string MacDomain(RotationKeyPurpose purpose) => purpose switch
        {
            RotationKeyPurpose.Session => "rt2",
            RotationKeyPurpose.LocationReport => "rt2-loc",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null),
        };

        private byte[] ComputeRotationMac(string sessionKey, int counter, string salt, RotationKeyPurpose purpose) =>
            HMACSHA256.HashData(_refreshKey, Encoding.UTF8.GetBytes($"{MacDomain(purpose)}|{sessionKey}|{counter}|{salt}"));

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
