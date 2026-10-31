using api_server.Auth;
using api_server.Auth.Dto;
using api_server.Controllers.Base;
using db_model.UserManagement;
using exs.modelCommons.UserManagement;
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
        // /api/auth/login is kept as an alias of the portal login for existing clients.
        [HttpPost("/api/auth/portal_login")]
        [HttpPost("/api/auth/login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")] // brute-force guard; policy defined in Program.cs
        public Task<IActionResult> PortalLogin([FromBody] LoginData request, CancellationToken cancellationToken) =>
            LoginCommon(request, UserSessionSource.Login_Portal, cancellationToken);

        [HttpPost("/api/auth/app_login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")] // same bucket as the portal login: one budget per client IP
        public Task<IActionResult> AppLogin([FromBody] LoginData request, CancellationToken cancellationToken) =>
            LoginCommon(request, UserSessionSource.Login_App, cancellationToken);

        private async Task<IActionResult> LoginCommon(LoginData request, short userSessionSourceId, CancellationToken cancellationToken)
        {
            var result = await authService.LoginAsync(
                request.UserName, request.Password, request.RememberMe ?? false, userSessionSourceId, request.ClientSideInfo,
                HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            if (result.Status == AuthStatus.TooManyAttempts)
            {
                // Same answer for every name typed, real or not, so this does not reveal accounts.
                Response.Headers.RetryAfter = ((int)Math.Ceiling(result.RetryAfter!.Value.TotalSeconds)).ToString();
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { message = "Too many failed login attempts. Try again later." });
            }
            if (!result.Succeeded)
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }

            SetRefreshCookie(result);
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

        // Creates a JustCreated user and delivers a password reset key; the account becomes
        // Active when the user sets their password through /api/auth/reset-password.
        // /api/auth/register is kept as an alias of the portal registration for existing clients.
        [HttpPost("/api/auth/portal_register")]
        [HttpPost("/api/auth/register")]
        [AllowAnonymous]
        [EnableRateLimiting("register")] // own bucket: must not eat into the login budget on a shared IP
        public Task<IActionResult> PortalRegister([FromBody] RegisterUserData request, CancellationToken cancellationToken) =>
            RegisterCommon(request, UserSource.Registration_Portal, cancellationToken);

        [HttpPost("/api/auth/app_register")]
        [AllowAnonymous]
        [EnableRateLimiting("register")] // same bucket as the portal registration: one budget per client IP
        public Task<IActionResult> AppRegister([FromBody] RegisterUserData request, CancellationToken cancellationToken) =>
            RegisterCommon(request, UserSource.Registration_App, cancellationToken);

        private async Task<IActionResult> RegisterCommon(RegisterUserData request, short userSourceId, CancellationToken cancellationToken)
        {
            var result = await authService.RegisterUserAsync(request, userSourceId, request.ClientSideInfo,
                HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            if (result.Status == RegisterUserStatus.Success)
            {
                return NoContent();
            }

            foreach (var (field, messages) in result.Errors!)
            {
                foreach (var message in messages)
                {
                    ModelState.AddModelError(field, message);
                }
            }
            return result.Status == RegisterUserStatus.Conflict
                ? ValidationProblem(modelStateDictionary: ModelState, statusCode: StatusCodes.Status409Conflict)
                : ValidationProblem(ModelState);
        }

        // Always 204, whether or not a matching user exists — the response must not reveal
        // which accounts are registered. The key is delivered out-of-band, never returned.
        [HttpPost("/api/auth/request-password-reset")]
        [AllowAnonymous]
        [EnableRateLimiting("password-reset-request")] // triggers outbound e-mail; per-account cooldown is in AuthService
        public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetData request, CancellationToken cancellationToken)
        {
            await authService.RequestPasswordResetAsync(request.Identifier, cancellationToken);
            return NoContent();
        }

        [HttpPost("/api/auth/reset-password")]
        [AllowAnonymous]
        [EnableRateLimiting("password-reset")] // guessing guard for reset keys
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordData request, CancellationToken cancellationToken)
        {
            var result = await authService.ResetPasswordAsync(request.ResetKey, request.NewPassword, cancellationToken);

            switch (result.Status)
            {
                case ResetPasswordStatus.Success:
                    return NoContent();
                case ResetPasswordStatus.PasswordRejected:
                    foreach (var error in result.PasswordErrors!)
                    {
                        ModelState.AddModelError(nameof(request.NewPassword), error);
                    }
                    return ValidationProblem(ModelState);
                case ResetPasswordStatus.IdentifierTaken:
                    return Conflict(new { message = "This e-mail address or user name is already used by another account." });
                default:
                    return BadRequest(new { message = "The password reset key is invalid or has expired." });
            }
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

            SetRefreshCookie(result);
            if (returnTokenOnly) return Ok(result.Response?.AccessToken);
            else return Ok(result.Response);
        }

        private string? ReadRotationKey() =>
            Request.Cookies.TryGetValue(tokenService.RefreshCookieName, out var key) ? key : null;

        private void SetRefreshCookie(AuthResult result) =>
            Response.Cookies.Append(tokenService.RefreshCookieName, result.RotationKey!,
                tokenService.BuildRefreshCookieOptions(Request.IsHttps, result.CookieExpiresUtc));

        private void ClearRefreshCookie() =>
            Response.Cookies.Delete(tokenService.RefreshCookieName,
                new CookieOptions { Path = TokenService.RefreshCookiePath });
    }
}
