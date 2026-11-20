using api_server.Auth;
using api_server.Auth.Dto;
using api_server.Controllers.Auth.Dto;
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
        
        [HttpPost("/api/auth_cookie/portal_login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")] // brute-force guard; policy defined in Program.cs
        [ProducesResponseType(typeof(LoginResponseData), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public Task<IActionResult> PortalLogin([FromBody] LoginRequestData request, CancellationToken cancellationToken) =>
            LoginCommon(request, UserSessionSource.Login_Portal, useCookie: true, autoLoginMode:false, cancellationToken);

        // No cookie here: the app keeps the key in its own secure storage and gets it in the body.
        [HttpPost("/api/auth/app_login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")] // same bucket as the portal login: one budget per client IP
        [ProducesResponseType(typeof(LoginResponseData), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public Task<IActionResult> AppLogin([FromBody] LoginRequestData request, CancellationToken cancellationToken) =>
            LoginCommon(request, UserSessionSource.Login_App, useCookie: false, autoLoginMode:false, cancellationToken);
        // No cookie here: the app keeps the key in its own secure storage and gets it in the body.
        [HttpPost("/api/auth/app_auto_login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")] // same bucket as the portal login: one budget per client IP
        [ProducesResponseType(typeof(LoginResponseData), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public Task<IActionResult> AppAutoLogin([FromBody] LoginRequestData request, CancellationToken cancellationToken) =>
            LoginCommon(request, UserSessionSource.Login_App, useCookie: false, autoLoginMode: true, cancellationToken);
        private async Task<IActionResult> LoginCommon(LoginRequestData request, short userSessionSourceId, bool useCookie, bool autoLoginMode, CancellationToken cancellationToken)
        {
            var (status, result)  = await authService.LoginAsync(
                request.UserName, request.Password, request.RememberMe ?? false, userSessionSourceId, request.GpsDeviceSerial, request.ClientSideInfo,
                HttpContext.Connection.RemoteIpAddress?.ToString(), autoLoginMode, cancellationToken);
            if (status == LoginStatus.TooManyAttempts)
            {
                // Same answer for every name typed, real or not, so this does not reveal accounts.
                //Response.Headers.RetryAfter = ((int)Math.Ceiling(result.RetryAfter!.Value.TotalSeconds)).ToString();
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { message = "Too many failed login attempts. Try again later." });
            }
            if (status == LoginStatus.GpsDeviceFromAnotherUser)
            {
                // Only returned after the password was verified, so this does not reveal which accounts exist.
                return Conflict(new
                {
                    code = "gps_device_belongs_to_another_user",
                    message = "This device is registered to another user."
                });
            }
            if ( status == LoginStatus.Success && result != null)
            {
                if (useCookie)
                {
                    SetRefreshCookie(result);
                    return Ok(new LoginResponseData
                    {
                        AccessToken = result.AccessToken,
                        UserSessionInfo = new UserSessionInfoDto
                        {
                            UserId = result.UserId
                        }
                    });
                }
                else 
                return Ok(new LoginResponseData
                {
                    SessionKey = result.SessionKey,
                    RotationCounter = result.RotationCounter,
                    SessionSign = result.SessionSign,
                    AccessToken = result.AccessToken,
                    UserSessionInfo = new UserSessionInfoDto
                    {
                        UserId = result.UserId,
                        CompanyId = result.CompanyId
                    }
                });

            }
            else return Unauthorized(new { message = "Invalid username or password." });
        }

        [HttpPost("/api/auth_cookie/check-session")]
        [AllowAnonymous]
        [EnableRateLimiting("refresh")] // unauthenticated + writes to the DB every call
        [ProducesResponseType(typeof(CheckSessionResponseData), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CheckSessionCookie([FromBody] CheckSessionRequestData requestData, CancellationToken cancellationToken)
        {
            var result = await authService.RefreshCookieTokenAsync(ReadCookieKey(), requestData.ClientSideInfo, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            if ( result != null )
            {
                SetRefreshCookie(result);
                return Ok(new CheckSessionResponseData
                {
                    AccessToken = result.AccessToken,
                    UserSessionInfo = new UserSessionInfoDto
                    {
                        UserId = result.UserId
                    }
                });
            }
            else
            {
                ClearRefreshCookie();
                return Unauthorized();
            }

        }
        [HttpPost("/api/auth/check-session")]
        [AllowAnonymous]
        [EnableRateLimiting("refresh")] // unauthenticated + writes to the DB every call
        [ProducesResponseType(typeof(CheckSessionResponseData), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CheckSession([FromBody] CheckSessionRequestData requestData, CancellationToken cancellationToken)
        {
            var result = await authService.RefreshTokenAsync(requestData.SessionKey??"", requestData.RotationCounter??0, requestData.SessionSign??"", requestData.ClientSideInfo, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            if (result != null)
            {
                return Ok(new CheckSessionResponseData
                {
                    AccessToken = result.AccessToken,
                    RotationCounter = result.RotationCounter,
                    SessionSign = result.SessionSign,
                    UserSessionInfo = new UserSessionInfoDto
                    {
                        UserId = result.UserId,
                        CompanyId = result.CompanyId
                    }
                });
            }
            else
            {
                return Unauthorized();
            }

        }
        [HttpPost("/api/auth_cookie/refresh")]
        [AllowAnonymous]
        [EnableRateLimiting("refresh")] // unauthenticated + writes to the DB every call
        [ProducesResponseType(typeof(RefreshKeyResponseData), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshSessionCookie( CancellationToken cancellationToken)
        {
            var result = await authService.RefreshCookieTokenAsync(ReadCookieKey(), null, null, cancellationToken);
            if (result != null)
            {
                SetRefreshCookie(result);
                return Ok(new RefreshKeyResponseData
                {
                    AccessToken = result.AccessToken,
                    //RotationCounter = result.RotationCounter,
                    //SessionSign = result.SessionSign
                });
            }
            else
            {
                ClearRefreshCookie();
                return Unauthorized();
            }
        }
        [HttpPost("/api/auth/refresh")]
        [AllowAnonymous]
        [EnableRateLimiting("refresh")] // unauthenticated + writes to the DB every call
        [ProducesResponseType(typeof(RefreshKeyResponseData), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshSession([FromBody] RefreshKeyRequestData requestData, CancellationToken cancellationToken)
        {
            var result = await authService.RefreshTokenAsync(requestData.SessionKey, requestData.RotationCounter, requestData.SessionSign, null, null, cancellationToken);
            if (result != null)
            {
                return Ok(new RefreshKeyResponseData
                {
                    AccessToken = result.AccessToken,
                    RotationCounter = result.RotationCounter,
                    SessionSign = result.SessionSign
                });
            }
            else
            {
                return Unauthorized();
            }
        }
        [HttpPost("/api/auth_cookie/logout")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            await authService.LogoutCookieAsync(ReadCookieKey(), cancellationToken);
            ClearRefreshCookie();
            return Ok();
        }
        [HttpPost("/api/auth/logout")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Logout([FromBody] LogOutRequestData requestData, CancellationToken cancellationToken)
        {
            await authService.LogoutAsync(requestData.SessionKey, cancellationToken);
            ClearRefreshCookie();
            return Ok();
        }

        // Creates a JustCreated user and delivers a password reset key; the account becomes
        // Active when the user sets their password through /api/auth/reset-password.
        // /api/auth/register is kept as an alias of the portal registration for existing clients.
        [HttpPost("/api/user/portal_register")]
        [HttpPost("/api/user/register")]
        [AllowAnonymous]
        [EnableRateLimiting("register")] // own bucket: must not eat into the login budget on a shared IP
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
        public Task<IActionResult> PortalRegister([FromBody] RegisterUserData request, CancellationToken cancellationToken) =>
            RegisterCommon(request, UserSource.Registration_Portal, cancellationToken);

        [HttpPost("/api/user/app_register")]
        [AllowAnonymous]
        [EnableRateLimiting("register")] // same bucket as the portal registration: one budget per client IP
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
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
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetData request, CancellationToken cancellationToken)
        {
            await authService.RequestPasswordResetAsync(request.Identifier, cancellationToken);
            return NoContent();
        }

        [HttpPost("/api/auth/reset-password")]
        [AllowAnonymous]
        [EnableRateLimiting("password-reset")] // guessing guard for reset keys
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
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




        private string ReadCookieKey() =>
            Request.Cookies.TryGetValue(tokenService.RefreshCookieName, out var key) ? key : "";

        private void SetRefreshCookie(SessionCheckResult result) =>
            Response.Cookies.Append(tokenService.RefreshCookieName, result.CompositeCookieKey!,
                tokenService.BuildRefreshCookieOptions(Request.IsHttps, result.CookieExpiresUtc));

        private void ClearRefreshCookie() =>
            Response.Cookies.Delete(tokenService.RefreshCookieName,
                tokenService.BuildRefreshCookieOptions(Request.IsHttps, null)); // same attributes as when it was set
    }
}
