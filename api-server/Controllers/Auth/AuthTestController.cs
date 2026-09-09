using System.IdentityModel.Tokens.Jwt;
using api_server.Controllers.Base;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Auth
{
    public class AuthTestController(ILogger<AuthTestController> logger) : AuthAPIController
    {
        // [Authorize], inherited from AuthAPIController, rejects an expired/invalid
        // access-token cookie with 401 before this method ever runs, so the log line
        // below only fires for a currently-valid token.
        [HttpGet("/api/authtest/tokentest")]
        public IActionResult TokenTest()
        {
            var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var sessionId = User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;
            logger.LogInformation("TokenTest: UserId {UserId}, SessionId {SessionId}", userId, sessionId);
            return Ok(new { userId, sessionId });
        }
    }
}
