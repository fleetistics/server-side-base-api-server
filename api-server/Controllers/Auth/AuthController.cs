using api_server.Auth;
using api_server.Auth.Dto;
using api_server.Controllers.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_server.Controllers.Auth
{
    // Inherits AuthAPIController (not just BaseAPIController) even though most
    // actions here are anonymous: the class-level [Authorize] it carries is the
    // secure-by-default posture used everywhere else in the API — a future
    // action added here without an explicit [AllowAnonymous] fails closed
    // instead of silently becoming public, same as every other controller.
    public class AuthController(AuthService authService, TokenService tokenService) : AuthAPIController
    {
        [HttpPost("/api/auth/login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")] // brute-force guard; policy defined in Program.cs
        public async Task<IActionResult> Login([FromBody] LoginData request, CancellationToken cancellationToken)
        {
            var result = await authService.LoginAsync(request.UserName, request.Password, cancellationToken);
            if (!result.Succeeded)
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }

            SetRefreshCookie(result.RotationKey!);
            return Ok(result.Response);
        }

        // Called on SPA start-up and whenever an access token expires. The SPA
        // sends nothing itself — the rotation key travels in the httpOnly cookie.
        [HttpPost("/api/auth/refresh")]
        [AllowAnonymous]
        [EnableRateLimiting("refresh")] // unauthenticated + writes to the DB every call
        public async Task<IActionResult> Refresh(ClientSideInfo clientSideInfo, CancellationToken cancellationToken)
        {
            var result = await authService.RefreshLoginAsync(ReadRotationKey(), clientSideInfo, cancellationToken);
            return HandleSessionResult(result, true);
        }

       [HttpPost("/api/auth/check-session")]
       [AllowAnonymous]
       [EnableRateLimiting("refresh")] // unauthenticated + writes to the DB every call
        public async Task<IActionResult> CheckSession( ClientSideInfo clientSideInfo, CancellationToken cancellationToken)
        {
            var result = await authService.RefreshLoginAsync(ReadRotationKey(), clientSideInfo, cancellationToken);
            return HandleSessionResult(result, false);
        }

        [HttpPost("/api/auth_auto/refresh")]
        [AllowAnonymous]
        [EnableRateLimiting("refresh")] // unauthenticated + writes to the DB every call
        public async Task<IActionResult> RefreshAutoDevice( ClientSideInfo clientSideInfo, CancellationToken cancellationToken)
        {
            var result = await authService.RefreshAutoDeviceAsync(clientSideInfo, cancellationToken);
            if (result.Succeeded) return Ok(result.Response?.AccessToken);
            else return Unauthorized();
        }
        [HttpPost("/api/auth_auto/check-session")]
        [AllowAnonymous]
        [EnableRateLimiting("refresh")] // unauthenticated + writes to the DB every call
        public async Task<IActionResult> CheckSessionAutoDevice(ClientSideInfo clientSideInfo, CancellationToken cancellationToken)
        {
            var result = await authService.RefreshAutoDeviceAsync(clientSideInfo, cancellationToken);
            if (result.Succeeded) return Ok(result.Response);
            else return Unauthorized();
        }

        [HttpPost("/api/auth/logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            await authService.LogoutAsync(ReadRotationKey(), cancellationToken);
            ClearRefreshCookie();
            return Ok();
        }

        // [Authorize] here is inherited from AuthAPIController's class-level attribute
        // (every other action opts out via [AllowAnonymous]) — this is the one action
        // that actually needs it.
        [HttpGet("/api/auth/me")]
        public IActionResult Me()
        {
            return Ok(new
            {
                userId = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value,
                sessionId = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sid)?.Value,
            });
        }

        private IActionResult HandleSessionResult(AuthResult result, bool returnTokenOnly)
        {
            if (!result.Succeeded)
            {
                if (result.ClearCookie)
                {
                    ClearRefreshCookie();
                }
                return Unauthorized();
            }

            SetRefreshCookie(result.RotationKey!);
            if (returnTokenOnly) return Ok(result.Response?.AccessToken);
            else return Ok(result.Response);
        }

        private string? ReadRotationKey() =>
            Request.Cookies.TryGetValue(tokenService.RefreshCookieName, out var key) ? key : null;

        private void SetRefreshCookie(string rotationKey) =>
            Response.Cookies.Append(tokenService.RefreshCookieName, rotationKey,
                tokenService.BuildRefreshCookieOptions(Request.IsHttps));

        private void ClearRefreshCookie() =>
            Response.Cookies.Delete(tokenService.RefreshCookieName,
                new CookieOptions { Path = TokenService.RefreshCookiePath });
    }
}
