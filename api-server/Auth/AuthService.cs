using api_server.Auth.Dto;
using api_server.core;
using db_model.AppStructure;
using db_model.Gps;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace api_server.Auth
{
    public enum AuthStatus
    {
        Success,
        InvalidCredentials,
        Unauthenticated,
    }

    /// <param name="RotationKey">New rotation key the caller should write to the cookie.</param>
    /// <param name="ClearCookie">True when the caller should delete the refresh cookie.</param>
    public sealed record AuthResult(
        AuthStatus Status,
        UserSessionCheckResponse? Response = null,
        string? RotationKey = null,
        bool ClearCookie = false)
    {
        public bool Succeeded => Status == AuthStatus.Success;
    }

    /// <summary>
    /// Session and token logic for the auth endpoints. Deliberately free of
    /// HttpContext: cookie reading/writing stays in the controller, this class
    /// only takes and returns the rotation key.
    /// </summary>
    public class AuthService(
        TokenService tokenService,
        IRepository repository,
        ILogger<AuthService> logger)
    {
        public async Task<AuthResult> LoginAsync(string userName, string password, CancellationToken cancellationToken)
        {
            using var activity = Telemetry.Source.StartActivity("auth.login");

            var user = await repository
                .GetQueryable<User>(u => u.UserName == userName)
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null || user.StatusId != UserStatus.Active || !VerifyPassword(password, user.Password))
            {
                Telemetry.LoginFailures.Add(1);
                activity?.SetStatus(ActivityStatusCode.Error, "invalid credentials");
                logger.LogWarning("Failed login attempt for {UserName}", userName);
                return new AuthResult(AuthStatus.InvalidCredentials);
            }

            activity?.SetTag("app.user.id", user.Id);

            var now = DateTime.UtcNow;
            var session = new UserSession
            {
                UserId = user.Id,
                StatusId = UserSessionStatus.Active,
                Token = new SessionTokenInfo
                {
                    //SessionKey = TokenService.GenerateKey(),
                    SessionRotationKey = TokenService.GenerateKey(),
                    CreationTime = now,
                    ExpirationTime = now.AddDays(tokenService.RefreshTokenDays),
                    LatestRefreshTime = now,
                },
            };
            repository.Create(session);
            await repository.SaveAsync(cancellationToken);

            logger.LogInformation("Login succeeded for {UserName} (UserId {UserId}), session {SessionId}",
                user.UserName, user.Id, session.Id);

            return BuildSuccess(session);
        }

        /// <summary>Rotates the session and issues a new access token.</summary>

        private Task convertClientSideInfoToSessionClientInfo(ClientSideInfo clientSideInfo, SessionClientInfo sessionClientInfo)
        {
            sessionClientInfo.DeviceUID = clientSideInfo.DeviceUID;
            sessionClientInfo.FCMToken = clientSideInfo.FCMToken;
            sessionClientInfo.AppVersion = clientSideInfo.AppVersion;
            sessionClientInfo.CodeVersion = clientSideInfo.CodeVersion;
            sessionClientInfo.ClientDevicePlatformId = clientSideInfo.PlatformId;
            //switch (clientSideInfo.PlatformName.ToLower())
            //{
            //    case "ios":
            //        sessionClientInfo.ClientDevicePlatformId = ClientDevicePlatform.Ios;
            //        break;
            //    case "android":
            //        sessionClientInfo.ClientDevicePlatformId = ClientDevicePlatform.Android;
            //        break;
            //    case "windows":
            //        sessionClientInfo.ClientDevicePlatformId = ClientDevicePlatform.Windows;
            //        break;
            //    default:
            //        sessionClientInfo.ClientDevicePlatformId = ClientDevicePlatform.Unknown;
            //        break;
            //}
            return Task.CompletedTask;
        }
        private void refreshSessionClientInfo(ClientSideInfo clientSideInfo, SessionClientInfo sessionClientInfo)
        {
            sessionClientInfo.FCMToken = clientSideInfo.FCMToken;
            sessionClientInfo.AppVersion = clientSideInfo.AppVersion;
            sessionClientInfo.CodeVersion = clientSideInfo.CodeVersion;
        }
        private Task<short> convertClientSideInfoToGpsDeviceProvider(ClientSideInfo clientSideInfo, SessionClientInfo sessionClientInfo)
        {
            switch (sessionClientInfo.ClientDevicePlatformId)
            {
                case ClientDevicePlatform.Ios:
                    return Task.FromResult((short)GpsDeviceProvider.Ios);
                case ClientDevicePlatform.Android:
                    return Task.FromResult((short)GpsDeviceProvider.Android);
                default:
                    return Task.FromResult((short)GpsDeviceProvider.Unknown);
            }
        }
        public async Task<AuthResult> RefreshAutoDeviceAsync( ClientSideInfo clientSideInfo, CancellationToken cancellationToken)
        {
            using var activity = Telemetry.Source.StartActivity("auth.auto_device.refresh_session");

            if (string.IsNullOrEmpty(clientSideInfo.DeviceUID))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "no DeviceUID");
                return new AuthResult(AuthStatus.Unauthenticated);
            }
            var session = await repository
                .GetQueryable<UserSession>(s => s.ClientInfo.DeviceUID == clientSideInfo.DeviceUID && s.StatusId == UserSessionStatus.Active)
                .FirstOrDefaultAsync(cancellationToken);
            var now = DateTime.UtcNow;
            if (session is null)
            {
                var user = await repository.GetQueryable<User>(e => e.UserSourceId == UserSource.AUTO_DeviceUID && e.UserName == clientSideInfo.DeviceUID).FirstOrDefaultAsync(cancellationToken);
                var gpsDevice = await repository.GetQueryable<MobileGpsDevice>(e => e.DeviceUID == clientSideInfo.DeviceUID).FirstOrDefaultAsync(cancellationToken);
                if (user is null)
                {
                    user = new User
                    {
                        DisplayName = clientSideInfo.DeviceUID,
                        FullName = clientSideInfo.DeviceUID,
                        UserName = clientSideInfo.DeviceUID,
                        UserSourceId = UserSource.AUTO_DeviceUID,
                        StatusId = UserStatus.JustCreated
                    };
                    repository.Create(user);
                    await repository.SaveAsync(cancellationToken);
                    user.DisplayName = "User_" + user.Id;
                    user.FullName = user.DisplayName;
                    user.StatusId = UserStatus.Active;
                    activity?.SetTag("app.auto_device.new_user", true);
                }
                else
                {
                    if (user.StatusId != UserStatus.Active)
                    {
                        activity?.SetStatus(ActivityStatusCode.Error, "user not active");
                        return new AuthResult(AuthStatus.Unauthenticated);
                    }
                }

                session = new UserSession
                {
                    UserId = user.Id,
                    StatusId = UserSessionStatus.Active,
                    SessionSourceId = UserSessionSource.AUTO_DeviceUID,
                };
                repository.Create(session);
                await convertClientSideInfoToSessionClientInfo(clientSideInfo, session.ClientInfo);
                session.Token.ExpirationTime = now.AddDays(tokenService.RefreshTokenDays);
                if (gpsDevice is null)
                {
                    gpsDevice = new MobileGpsDevice
                    {
                        DeviceUID = clientSideInfo.DeviceUID,
                        UserId = user.Id,
                        ProviderId = await convertClientSideInfoToGpsDeviceProvider(clientSideInfo, session.ClientInfo),
                        StatusId = GpsDeviceStatus.Active
                    };
                    repository.Create(gpsDevice);
                    session.MobileGpsDevice = gpsDevice;
                }
                else
                {
                    if (gpsDevice.StatusId != GpsDeviceStatus.Active)
                    {
                        activity?.SetStatus(ActivityStatusCode.Error, "gpsDevice not active");
                        return new AuthResult(AuthStatus.Unauthenticated);
                    }
                }
                activity?.SetTag("app.auto_device.new_session", true);
            }
            else
            {
                activity?.SetTag("app.auto_device.new_user", false);
                activity?.SetTag("app.auto_device.new_session", false);
                if (session.Token.ExpirationTime <= now) session.Token.ExpirationTime = now.AddDays(tokenService.RefreshTokenDays);
            }
            refreshSessionClientInfo(clientSideInfo, session.ClientInfo);

            //RotateAsync will update UserSession in DB too
            return await RotateAsync(session, activity, cancellationToken);
        }
        //public Task<AuthResult> RefreshAsync(string? rotationKey, ClientSideInfo? clientSideInfo, CancellationToken cancellationToken) =>
        //    RotateSessionAsync(rotationKey, clientSideInfo, cancellationToken);

        ///// <summary>
        ///// Same as <see cref="RefreshAsync"/>, additionally recording which app
        ///// build the session is being used from.
        ///// </summary>
        //public Task<AuthResult> CheckSessionAsync(string? rotationKey, ClientSideInfo? clientSideInfo, CancellationToken cancellationToken) =>
        //    RotateSessionAsync(rotationKey, clientSideInfo, cancellationToken);

        public async Task LogoutAsync(string? rotationKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(rotationKey))
            {
                return;
            }

            var session = await FindActiveSessionAsync(rotationKey, cancellationToken);
            if (session is not null)
            {
                session.StatusId = UserSessionStatus.LoggedOutByUser;
                await repository.SaveAsync(cancellationToken);
                logger.LogInformation("Session {SessionId} logged out", session.Id);
            }
        }

        public async Task<AuthResult> RefreshLoginAsync(string? rotationKey, ClientSideInfo clientSideInfo, CancellationToken cancellationToken)
        {
            using var activity = Telemetry.Source.StartActivity("auth.login.refresh_session");

            if (string.IsNullOrWhiteSpace(rotationKey))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "no rotation key");
                return new AuthResult(AuthStatus.Unauthenticated);
            }

            var session = await FindActiveSessionAsync(rotationKey, cancellationToken);
            if (session is not null)
            {
                return await RotateAsync(session, activity, cancellationToken);
            }

            // Not the current key. If it's the one we JUST superseded (inside the
            // reuse-grace window), this is almost certainly a harmless race — two tabs,
            // an app relaunch, a retried request — not theft: hand back the session's
            // current state without rotating again. Past the grace window, an old key
            // showing up is the real signal — the legitimate holder already moved on,
            // so this is a stolen copy being replayed.
            var superseded = await FindRecentlySupersededSessionAsync(rotationKey, cancellationToken);
            if (superseded is not null && superseded.Token.ExpirationTime > DateTime.UtcNow)
            {
                activity?.SetTag("app.session.reuse_grace_window_hit", true);
                return BuildSuccess(superseded);
            }

            activity?.SetStatus(ActivityStatusCode.Error, "rotation key not active");
            logger.LogWarning("Session rotation rejected: rotation key not active");
            return new AuthResult(AuthStatus.Unauthenticated, ClearCookie: true);
        }

        private async Task<AuthResult> RotateAsync(UserSession session, Activity? activity, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            if (session.Token.ExpirationTime <= now)
            {
                session.StatusId = UserSessionStatus.Expired;
                await repository.SaveAsync(cancellationToken);

                logger.LogInformation("Session rotation rejected: session {SessionId} expired", session.Id);
                activity?.SetStatus(ActivityStatusCode.Error, $"Session rotation rejected: session {session.Id} expired");
                return new AuthResult(AuthStatus.Unauthenticated, ClearCookie: true);
            }

            // Rotate: the old key still works for the reuse-grace window, then it's dead for good.
            session.Token.PreviousSessionRotationKey = session.Token.SessionRotationKey;
            session.Token.SessionRotationKey = TokenService.GenerateKey();
            session.Token.LatestRefreshTime = now;
            await repository.SaveAsync(cancellationToken);

            return BuildSuccess(session);
        }

        private Task<UserSession?> FindActiveSessionAsync(string rotationKey, CancellationToken cancellationToken) =>
            repository
                .GetQueryable<UserSession>(s => s.Token.SessionRotationKey == rotationKey && s.StatusId == UserSessionStatus.Active)
                .FirstOrDefaultAsync(cancellationToken);

        private Task<UserSession?> FindRecentlySupersededSessionAsync(string rotationKey, CancellationToken cancellationToken)
        {
            var cutoff = DateTime.UtcNow - tokenService.RefreshReuseGraceWindow;
            return repository
                .GetQueryable<UserSession>(s =>
                    s.Token.PreviousSessionRotationKey == rotationKey &&
                    s.StatusId == UserSessionStatus.Active &&
                    s.Token.LatestRefreshTime >= cutoff)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private AuthResult BuildSuccess(UserSession session) =>
            new(AuthStatus.Success,
                new UserSessionCheckResponse
                {
                    AccessToken = tokenService.CreateAccessToken(session.UserId.ToString(), session.Id.ToString(), session.MobileGpsDeviceId?.ToString()),
                    UserId = session.UserId,
                    SessionId = session.Id,
                },
                session.Token.SessionRotationKey);

        // TODO: the stored value is compared verbatim — passwords are held in
        // plain text in "user"."Password". Swap this for a salted hash
        // (PBKDF2/Argon2) before any real deployment; it is isolated here so
        // that change touches one method.
        private static bool VerifyPassword(string supplied, string stored) =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(supplied),
                Encoding.UTF8.GetBytes(stored));
    }
}
