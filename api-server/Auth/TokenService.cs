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

        private readonly JwtOptions _options = jwtOptions.Value;

        public string CreateAccessToken(string userId, string sessionId, string? mobileGpsDeviceId = null, IEnumerable<string>? roles = null)
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

        // 256 bits of entropy, URL-safe. Used for both SessionKey (stable per
        // login) and SessionRotationKey (replaced on every refresh).
        public static string GenerateKey() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

        // Scoped to /api/auth only: the SPA never needs to send this cookie to
        // ordinary API endpoints, so narrowing the path shrinks the exposure
        // if any endpoint were ever vulnerable to XSS/CSRF.
        public CookieOptions BuildRefreshCookieOptions(bool isHttps)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = isHttps,
                SameSite = SameSiteMode.Lax,
                Path = RefreshCookiePath,
                Expires = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays),
            };
        }

        public const string RefreshCookiePath = "/api/auth";

        public string RefreshCookieName => _options.RefreshCookieName;
        public int RefreshTokenDays => _options.RefreshTokenDays;
        public TimeSpan RefreshReuseGraceWindow => TimeSpan.FromSeconds(_options.RefreshReuseGraceSeconds);
    }
}
