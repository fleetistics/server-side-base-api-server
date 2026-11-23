using api_server.Auth.Dto;
using api_server.Controllers.Auth.Dto;
using api_server.core;
using db_model.Gps;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using exs.modelCommons.AppStructure;
using exs.modelCommons.UserManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace api_server.Auth
{
    public enum LoginStatus
    {
        Success,
        InvalidCredentials,
        /// <summary>The account is temporarily locked after repeated failed logins.</summary>
        TooManyAttempts,
        /// <summary>The password was right, but the mobile device serial is registered to another user.</summary>
        GpsDeviceFromAnotherUser
    }

    /// <summary>
    /// A successful login or session refresh: the refresh key (as separate fields and as the combined cookie
    /// value), the access token and the user id. <c>CookieExpiresUtc</c> is null for a session cookie.
    /// The key fields and the access token are secrets, so ToString is overridden to leave them out.
    /// </summary>
    public sealed record SessionCheckResult(string SessionKey, int RotationCounter, string SessionSign, string CompositeCookieKey, DateTime? CookieExpiresUtc, int UserId, int? CompanyId, string AccessToken, RefreshAppLocationTokenResponseData? AppLocationTokenInfo = null)
    {
        public override string ToString() => $"{nameof(SessionCheckResult)} {{ UserId = {UserId}, CompanyId = {CompanyId} }}";
    }

    public enum RegisterUserStatus
    {
        Success,
        Invalid,
        Conflict,
    }

    /// <param name="Errors">Field name → explanations (Invalid and Conflict only).</param>
    public sealed record RegisterUserResult(RegisterUserStatus Status, IReadOnlyDictionary<string, string[]>? Errors = null);

    public enum ResetPasswordStatus
    {
        Success,
        /// <summary>Unknown user, wrong key, no pending key or expired key — deliberately indistinguishable.</summary>
        InvalidResetKey,
        PasswordRejected,
        /// <summary>Activating a new registration, but an Active user already holds its e-mail / user name.</summary>
        IdentifierTaken,
    }

    /// <param name="PasswordErrors">Explanations of every password rule that failed (PasswordRejected only).</param>
    public sealed record ResetPasswordResult(ResetPasswordStatus Status, IReadOnlyList<string>? PasswordErrors = null)
    {
        public bool Succeeded => Status == ResetPasswordStatus.Success;
    }

    /// <summary>
    /// Session and token logic for the auth endpoints. Deliberately free of
    /// HttpContext: cookie reading/writing stays in the controller, this class
    /// only takes and returns the rotation key.
    /// </summary>
    public class AuthService(
        TokenService tokenService,
        IRepository repository,
        IOptions<AuthOptions> authOptions,
        PasswordHasher passwordHasher,
        IPasswordResetKeySender resetKeySender,
        LoginThrottle loginThrottle,
        ILogger<AuthService> logger)
    {
        // Null when the session has no location chain to hand out: it is not bound to a mobile GPS device (the
        // location report needs one, see LocationAuthAPIController), or its location counter was never started.
        // Handing out a key for a counter the row does not hold would make its first use look like a replay.
        private RefreshAppLocationTokenResponseData? generateAppLocationTokenResponseData(UserSession session)
        {
            if (session.MobileGpsDeviceId is not int mobileGpsDeviceId || session.Token.LocationReportTokenRotationCounter is not int locationCounter)
            {
                return null;
            }
            var LocationReportKey = tokenService.CreateRotationKey(session.SessionKey, locationCounter, session.Token.KeySalt, RotationKeyPurpose.LocationReport);
            return new RefreshAppLocationTokenResponseData
            {
                access_token = tokenService.CreateAccessToken(session.UserId.ToString(), session.Id.ToString(), mobileGpsDeviceId.ToString(), kind: AccessTokenKind.LocationReport),
                refresh_token = LocationReportKey.CompositeCookieKey,
                expires_in = tokenService.GetAccesstokenExpirationInSeconds()
            };
        }
        private SessionCheckResult generateSessionCheckResult(UserSession session, bool generateLocationReportToken=false)
        {
            var key = tokenService.CreateRotationKey(session.SessionKey, session.Token.RotationCounter, session.Token.KeySalt, RotationKeyPurpose.Session);
                return new SessionCheckResult(
                session.SessionKey,
                session.Token.RotationCounter,
                key.SessionSign,
                key.CompositeCookieKey,

                session.Token.IsPersistent ? session.Token.ExpirationTime : null,
                session.UserId,
                session.CompanyId,
                // The device claim stays on the API token too: API endpoints such as the emergency alert read it.
                tokenService.CreateAccessToken(session.UserId.ToString(), session.Id.ToString(), session.MobileGpsDeviceId?.ToString(), companyId: session.CompanyId?.ToString()),
                generateLocationReportToken?generateAppLocationTokenResponseData(session):null
                );
        }


        private readonly AuthOptions _authOptions = authOptions.Value;

        /// <param name="rememberMe">True: fixed lifetime and persistent cookie. False: idle-expiring session and session cookie.</param>
        /// <param name="clientIp">Part of the failed-login throttle key and of the log; null when unknown.</param>
        public async Task<(LoginStatus, SessionCheckResult?)> LoginAsync(string userName, string password, bool rememberMe, short userSessionSourceId, string? gpsDeviceSerial, ClientSideInfo clientSideInfo, string? clientIp, bool autoLoginMode, CancellationToken cancellationToken)
        {
            using var activity = Telemetry.Source.StartActivity("auth.login");

            var name = userName.Trim();
            clientIp ??= "unknown";
            var throttleKey = ThrottleKey(name, clientIp);

            // A locked name+IP pair refuses everything — the right password included — before any
            // lookup or hashing. The lock is per typed name and client IP, existing name or not, so
            // refusing early says nothing about whether the account exists. Keying by IP as well means
            // a stranger who knows a user name can only lock that name out for their own IP.
            var retryAfter = loginThrottle.GetRetryAfter(throttleKey);
            if (retryAfter is not null)
            {
                Telemetry.LoginFailures.Add(1);
                activity?.SetStatus(ActivityStatusCode.Error, "account locked");
                logger.LogWarning("Login refused for {UserName} from {ClientIp}: account locked for another {RetryAfterSeconds} s",
                    ForLog(name), clientIp, (int)Math.Ceiling(retryAfter.Value.TotalSeconds));
                return (LoginStatus.TooManyAttempts, null);
            }

            // Only Active users can log in, and only Active users hold a name (an Inactive account
            // may share its user name with a newer Active one), so the status is part of the lookup.
            // The UserName column has a case-insensitive collation, so == ignores case in both e-mail modes.
            var user = await repository
                .GetQueryable<User>(u => u.StatusId == UserStatus.Active && u.UserName == name)
                .FirstOrDefaultAsync(cancellationToken);
            if (user is null && autoLoginMode)
            {
                // Auto-login mode: the user name is a device UID, and the user may not exist yet. Create it
                // with no password (the device will set one) and log in. The UID is unique across all users,
                // so no prefixing is needed.
                user = new User
                {
                    UserName = name,
                    DisplayName = name,
                    FullName = name,
                    UserSourceId = UserSource.AUTO_DeviceUID,
                    StatusId = UserStatus.JustCreated,
                };
                repository.Create(user);
                await repository.SaveAsync(cancellationToken);
                user.DisplayName = "User_" + user.Id;
                user.FullName = user.DisplayName;
                user.Password = passwordHasher.Hash(password);
                user.StatusId = UserStatus.Active;
                logger.LogInformation("Auto-login created user {UserId} for device UID {UserName}", user.Id, ForLog(name));
                await repository.SaveAsync(cancellationToken);
            }
            else
            {
                // One Argon2 verification on every path — user found or not, password set or not,
                // right or wrong — so the response time says nothing about which of those it was.
                var passwordMatches = passwordHasher.VerifyEqualTime(user?.Password, password);

                if (user is null || !passwordMatches)
                {
                    Telemetry.LoginFailures.Add(1);
                    activity?.SetStatus(ActivityStatusCode.Error, "invalid credentials");

                    // The reason is for the log only; the caller always gets the same answer.
                    var reason = user is null ? "UnknownOrInactiveUser" : user.Password is null ? "NoPasswordSet" : "WrongPassword";
                    var (failures, lockedNow) = loginThrottle.RegisterFailure(throttleKey);
                    logger.LogWarning("Failed login attempt for {UserName} from {ClientIp}: {Reason} (UserId {UserId}, failure {FailureCount} of {MaxFailures})",
                        ForLog(name), clientIp, reason, user?.Id, failures, loginThrottle.MaxFailedAttempts);
                    if (lockedNow)
                    {
                        Telemetry.LoginLockouts.Add(1);
                        logger.LogWarning("Account {UserName} locked for {LockoutMinutes} min after {FailureCount} failed login attempts (last from {ClientIp})",
                            ForLog(name), _authOptions.LoginLockout.TotalMinutes, failures, clientIp);
                    }
                    return (LoginStatus.InvalidCredentials, null);
                }
            }
            loginThrottle.ResetKey(throttleKey);
            activity?.SetTag("app.user.id", user.Id);
            

            var now = DateTime.UtcNow;
            var isPersistent = autoLoginMode ? true : rememberMe;
            var session = new UserSession
            {
                UserId = user.Id,
                CompanyId = user.CompanyId,
                StatusId = UserSessionStatus.Active,
                SessionSourceId = userSessionSourceId,
                // 256 random bits, unique by the index on user_session; the first part of the refresh key.
                SessionKey = TokenService.GenerateKey(),
                Token = new SessionTokenInfo
                {
                    KeySalt = TokenService.GenerateKey(),
                    RotationCounter = 0,
                    IsPersistent = isPersistent,
                    CreationTime = now,
                    ExpirationTime = isPersistent
                        ? now.AddDays(tokenService.RefreshTokenDays)
                        : now + tokenService.SessionOnlyIdleLifetime,
                    LatestRefreshTime = now,
                },
            };
            session.ClientInfo.ClientApplicationId = clientSideInfo.AppId;
            session.ClientInfo.PlatformId = clientSideInfo.PlatformId;

            session.ClientInfo.AppVersion = clientSideInfo.AppVersion;
            session.ClientInfo.AppHash = clientSideInfo.AppHash;
            session.ClientInfo.PlatformDetails = PlatformDetailsJson.WithClientIp(clientSideInfo.PlatformDetails, clientIp);

            if ( !string.IsNullOrWhiteSpace(gpsDeviceSerial))
            {
                var devSerial = user.Id.ToString() + "-|-" + gpsDeviceSerial.Trim();
                var gpsDevice = await repository.GetQueryable<GpsDevice>(e => e.ProviderId == GpsDeviceProvider.Mobile && e.SerialNumber == devSerial).FirstOrDefaultAsync(cancellationToken);
                if (gpsDevice is null)
                {

                    int? gpsDeviceTypeId;
                    switch (session.ClientInfo.PlatformId)
                    {
                        case ClientDevicePlatform.Ios:
                            gpsDeviceTypeId = GpsDeviceType.MobileIos;
                            break;
                        case ClientDevicePlatform.Android:
                            gpsDeviceTypeId = GpsDeviceType.MobileAndroid;
                            break;
                        default:
                            gpsDeviceTypeId = null;
                            break;
                    }
                    gpsDevice = new GpsDevice
                    {
                        SerialNumber = devSerial,
                        UserId = user.Id,
                        CompanyId = user.CompanyId,
                        ProviderId = GpsDeviceProvider.Mobile,
                        TypeId = gpsDeviceTypeId,
                        StatusId = GpsDeviceStatus.Active
                    };
                    repository.Create(gpsDevice);
                    await repository.SaveAsync(cancellationToken);
                    session.MobileGpsDeviceId = gpsDevice.Id;
                }
                else if (gpsDevice.StatusId == GpsDeviceStatus.MobileLogOutInactive || gpsDevice.StatusId == GpsDeviceStatus.Active)
                {
                    if (user.Id != gpsDevice.UserId)
                    {
                        activity?.SetStatus(ActivityStatusCode.Error, $"gpsDevice belongs to another user or company Id {gpsDevice.Id} UserId {gpsDevice.UserId} CompanyId {gpsDevice.CompanyId}");
                        logger.LogWarning($"Login user {user.Id} gpsDevice belongs to another user or company Id {gpsDevice.Id} UserId {gpsDevice.UserId} CompanyId {gpsDevice.CompanyId}");
                        return (LoginStatus.GpsDeviceFromAnotherUser, null);
                    }
                    if (gpsDevice.StatusId == GpsDeviceStatus.MobileLogOutInactive)
                    {
                        gpsDevice.StatusId = GpsDeviceStatus.Active;
                        gpsDevice.CompanyId = user.CompanyId;
                        session.MobileGpsDeviceId = gpsDevice.Id;
                    }
                    else if (gpsDevice.StatusId == GpsDeviceStatus.Active)
                    {
                        session.MobileGpsDeviceId = gpsDevice.Id;
                        gpsDevice.CompanyId = user.CompanyId;
                    }

                }
                else
                {
                    activity?.SetStatus(ActivityStatusCode.Error, $"gpsDevice not active Id {gpsDevice.Id} StatusId {gpsDevice.StatusId}");
                    logger.LogWarning($"Login user {user.Id} gpsDevice not active Id {gpsDevice.Id} StatusId {gpsDevice.StatusId}");
                }



            }
            repository.Create(session);
            await repository.SaveAsync(cancellationToken);

            logger.LogInformation("Login succeeded for {UserName} (UserId {UserId}), session {SessionId}",
                user.UserName, user.Id, session.Id);

            return (LoginStatus.Success, generateSessionCheckResult(session));
        }

        public async Task<SessionCheckResult?> RefreshCookieTokenAsync(string cookieKey, ClientSideInfo? clientSideInfo, string? clientIp, CancellationToken cancellationToken)
        {
            if (TokenService.TryParseRotationKey(cookieKey) is RotationKey rotationKey)
            {
                return await RefreshTokenAsync(rotationKey.SessionKey, rotationKey.RotationCounter, rotationKey.SessionSign, false, clientSideInfo, clientIp, cancellationToken);
            }
            else return null;
        }

        /// <summary>
        /// Rotates the session the presented refresh key belongs to and issues a new access token.
        /// Null means the key was refused (unknown or ended session, forged, expired, replayed, or a
        /// concurrent refresh left nothing to hand back); the caller answers 401.
        /// </summary>
        /// <param name="clientSideInfo">Null for a plain refresh: the stored client info is left as it is.</param>
        /// <param name="clientIp">Added to the stored PlatformDetails JSON together with clientSideInfo; ignored when that is null.</param>
        public async Task<SessionCheckResult?> RefreshTokenAsync(string sessionKey, int rotationCounter, string sessionSign, bool doRefreshLocationReportToken, ClientSideInfo? clientSideInfo, string? clientIp, CancellationToken cancellationToken)
        {
            using var activity = Telemetry.Source.StartActivity("auth.refresh_session");

            // Auto-device sessions have no session key or salt, so no refresh key can ever match one.
            var session = await repository.GetQueryable<UserSession>(s => s.SessionKey == sessionKey && s.StatusId == UserSessionStatus.Active).FirstOrDefaultAsync(cancellationToken);
            if (session is null  || !tokenService.IsValidRotationMac(session.SessionKey, rotationCounter, session.Token.KeySalt, sessionSign, RotationKeyPurpose.Session))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "rotation key not valid");
                // The session key is a secret: neither it nor the presented key is logged.
                logger.LogWarning("Session rotation rejected: refresh key is not valid{Detail}", session is null ? " (unknown session)" : string.Empty);
                return null;
            }
            activity?.SetTag("app.session.id", session.Id);

            var now = DateTime.UtcNow;
            if (session.Token.ExpirationTime <= now)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "session expired");
                logger.LogWarning("Session rotation rejected: session {SessionId} expired at {ExpirationTimeUtc}", session.Id, session.Token.ExpirationTime);
                await markSessionExpiredAsync(session, cancellationToken);
                return null;
            }

            if (session.Token.RotationCounter == rotationCounter)
            {
                // Rotate: the previous counter's key still works for the reuse-grace window, then it's dead for good.
                //
                // One conditional UPDATE (compare-and-swap on this chain's counter) instead of a tracked save: it
                // touches only this chain's columns and bypasses the LatestUpdate concurrency token, so a location
                // rotation saved in the same instant no longer makes this one fail. Two rotations of the SAME chain
                // still exclude each other: only one of them finds the counter at the presented value.
                var startLocationChain = doRefreshLocationReportToken && session.MobileGpsDeviceId is not null;
                // Idle sessions live on while they are used; persistent ones keep their fixed expiry.
                DateTime? slidingExpiration = null;
                if (!session.Token.IsPersistent)
                {
                    var idleEnd = now + tokenService.SessionOnlyIdleLifetime;
                    var maxEnd = session.Token.CreationTime + tokenService.SessionOnlyMaxLifetime;
                    slidingExpiration = idleEnd < maxEnd ? idleEnd : maxEnd;
                }
                var platformDetails = clientSideInfo is null ? null : PlatformDetailsJson.WithClientIp(clientSideInfo.PlatformDetails, clientIp);

                var rotated = await repository
                    .GetQueryable<UserSession>(s => s.Id == session.Id && s.StatusId == UserSessionStatus.Active && s.Token.RotationCounter == rotationCounter)
                    .ExecuteUpdateAsync(setters =>
                    {
                        setters
                            .SetProperty(s => s.Token.RotationCounter, rotationCounter + 1)
                            .SetProperty(s => s.Token.LatestRefreshTime, now);
                        if (slidingExpiration is DateTime expiration)
                        {
                            setters.SetProperty(s => s.Token.ExpirationTime, expiration);
                        }
                        if (startLocationChain)
                        {
                            // Computed in SQL from the row's current value: a location refresh may have moved it since we read it.
                            setters
                                .SetProperty(s => s.Token.LocationReportTokenRotationCounter,
                                    s => s.Token.LocationReportTokenRotationCounter == null || s.Token.LocationReportTokenRotationCounter < 1
                                        ? 1
                                        : s.Token.LocationReportTokenRotationCounter + 1)
                                .SetProperty(s => s.Token.LocationReportTokenLatestRefreshTime, now);
                        }
                        if (clientSideInfo is not null)
                        {
                            setters
                                .SetProperty(s => s.ClientInfo.AppVersion, clientSideInfo.AppVersion)
                                .SetProperty(s => s.ClientInfo.AppHash, clientSideInfo.AppHash)
                                .SetProperty(s => s.ClientInfo.PlatformDetails, platformDetails);
                            // The DTO field is optional (the portal has no FCM), the column is NOT NULL: keep the stored value when none is sent.
                            if (clientSideInfo.FCM_FID is not null)
                            {
                                setters.SetProperty(s => s.ClientInfo.FCM_FID, clientSideInfo.FCM_FID);
                            }
                        }
                    }, cancellationToken) == 1;

                // The UPDATE bypassed the change tracker: read back what the row holds now, ours or the winner's.
                await repository.DbContext.Entry(session).ReloadAsync(cancellationToken);
                if (rotated)
                {
                    return generateSessionCheckResult(session, doRefreshLocationReportToken);
                }
                if (session.StatusId != UserSessionStatus.Active)
                {
                    return null;
                }
                // Another request rotated this chain first: decide below on the fresh values, exactly as for a key
                // that arrived late (inside the grace window it gets the winner's state, otherwise it is a replay).
                logger.LogInformation("Session {SessionId} rotation lost a race with a concurrent rotation", session.Id);
            }

            // The key we JUST superseded, inside the reuse-grace window: almost certainly a harmless race
            // (two tabs, an app relaunch, a retried request). Hand back the current state without rotating.
            if (rotationCounter == session.Token.RotationCounter - 1
                && session.Token.LatestRefreshTime >= now - tokenService.RefreshReuseGraceWindow
                && session.Token.ExpirationTime > now)
            {
                activity?.SetTag("app.session.reuse_grace_window_hit", true);
                return generateSessionCheckResult(session, doRefreshLocationReportToken);
            }

            Telemetry.SessionKeyReuseRevocations.Add(1);
            activity?.SetStatus(ActivityStatusCode.Error, "refresh key reused");
            logger.LogWarning("Session {SessionId} of user {UserId} revoked: refresh key with counter {PresentedCounter} replayed (current counter {CurrentCounter})",
                session.Id, session.UserId, rotationCounter, session.Token.RotationCounter);
            if (!await endSessionAsync(session, UserSessionStatus.RevokedKeyReuse, cancellationToken))
            {
                // Ended by someone else in the same instant (a logout, most likely); the caller is refused regardless.
                logger.LogInformation("Session {SessionId} had already ended when the revocation ran", session.Id);
            }
            return null;
        }
        public async Task<RefreshAppLocationTokenResponseData?> RefreshAppLocationTokenAsync(string? refresh_token, string? clientIp, CancellationToken cancellationToken)
        {
            if (TokenService.TryParseRotationKey(refresh_token) is RotationKey refreshKey)
            {
                using var activity = Telemetry.Source.StartActivity("auth.refresh_appLocation_token");

                // Auto-device sessions have no session key or salt, so no refresh key can ever match one.
                var session = await repository.GetQueryable<UserSession>(s => s.SessionKey == refreshKey.SessionKey && s.StatusId == UserSessionStatus.Active).FirstOrDefaultAsync(cancellationToken);
                if (session is null || !tokenService.IsValidRotationMac(session.SessionKey, refreshKey.RotationCounter, session.Token.KeySalt, refreshKey.SessionSign, RotationKeyPurpose.LocationReport))
                {
                    activity?.SetStatus(ActivityStatusCode.Error, "rotation key not valid");
                    // The session key is a secret: neither it nor the presented key is logged.
                    logger.LogWarning("Session rotation rejected: refresh key is not valid{Detail}", session is null ? " (unknown session)" : string.Empty);
                    return null;
                }
                activity?.SetTag("app.session.id", session.Id);

                var now = DateTime.UtcNow;
                if (session.Token.ExpirationTime <= now)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, "session expired");
                    logger.LogWarning("Session rotation rejected: session {SessionId} expired at {ExpirationTimeUtc}", session.Id, session.Token.ExpirationTime);
                    await markSessionExpiredAsync(session, cancellationToken);
                    return null;
                }

                if (session.Token.LocationReportTokenRotationCounter == refreshKey.RotationCounter)
                {
                    // Conditional UPDATE on this chain's counter only, as in RefreshTokenAsync: an API rotation saved in
                    // the same instant does not make it fail, a concurrent location rotation with the same key does.
                    var rotated = await repository
                        .GetQueryable<UserSession>(s => s.Id == session.Id && s.StatusId == UserSessionStatus.Active
                            && s.Token.LocationReportTokenRotationCounter == refreshKey.RotationCounter)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(s => s.Token.LocationReportTokenRotationCounter, refreshKey.RotationCounter + 1)
                            .SetProperty(s => s.Token.LocationReportTokenLatestRefreshTime, now),
                            cancellationToken) == 1;

                    // The UPDATE bypassed the change tracker: read back what the row holds now, ours or the winner's.
                    await repository.DbContext.Entry(session).ReloadAsync(cancellationToken);
                    if (rotated)
                    {
                        return generateAppLocationTokenResponseData(session);
                    }
                    if (session.StatusId != UserSessionStatus.Active)
                    {
                        return null;
                    }
                    // Another request rotated this chain first: decide below on the fresh values (grace window or replay).
                    logger.LogInformation("Session {SessionId} location rotation lost a race with a concurrent rotation", session.Id);
                }

                // The key we JUST superseded, inside the reuse-grace window: almost certainly a harmless race
                // (two tabs, an app relaunch, a retried request). Hand back the current state without rotating.
                if (refreshKey.RotationCounter == session.Token.LocationReportTokenRotationCounter - 1
                    && session.Token.LocationReportTokenLatestRefreshTime >= now - tokenService.RefreshReuseGraceWindow
                    && session.Token.ExpirationTime > now)
                {
                    activity?.SetTag("app.session.reuse_grace_window_hit", true);
                    return generateAppLocationTokenResponseData(session);
                }

                Telemetry.SessionKeyReuseRevocations.Add(1);
                activity?.SetStatus(ActivityStatusCode.Error, "refresh key reused");
                logger.LogWarning("Session {SessionId} of user {UserId} revoked: location refresh key with counter {PresentedCounter} replayed (current counter {CurrentCounter})",
                    session.Id, session.UserId, refreshKey.RotationCounter, session.Token.LocationReportTokenRotationCounter);
                if (!await endSessionAsync(session, UserSessionStatus.RevokedKeyReuse, cancellationToken))
                {
                    // Ended by someone else in the same instant (a logout, most likely); the caller is refused regardless.
                    logger.LogInformation("Session {SessionId} had already ended when the revocation ran", session.Id);
                }
            }
            return null;
        }

        // What the failure counter is keyed by: the typed name, compared the way login compares it
        // (case-insensitively). Very long input is hashed so a key stays small.
        private static string ThrottleKey(string name)
        {
            var key = name.ToLowerInvariant();
            return key.Length <= 256 ? key : "h:" + HashResetKey(key);
        }

        // Login's failure counter: one per typed name and client IP ("{name}|{ip}"). The name part is the
        // account-wide ThrottleKey, so LoginThrottle.Reset(ThrottleKey(name)) clears every IP's counter of it.
        private static string ThrottleKey(string name, string clientIp) => ThrottleKey(name) + "|" + clientIp;

        
        // The typed name goes into the log: strip control characters (no forged log lines) and
        // cap the length (the field is attacker-controlled).
        private static string ForLog(string value)
        {
            var clean = string.Concat(value.Where(c => !char.IsControl(c)));
            return clean.Length > 100 ? clean[..100] + "…" : clean;
        }


        /// <summary>Ends the active session with this session key. The key is a secret handle, so no mac or counter
        /// is needed: ending a session is the safe direction. An unknown or already ended session is ignored.</summary>
        public async Task LogoutAsync(string sessionKey, CancellationToken cancellationToken)
        {
            var session = await repository.GetQueryable<UserSession>(s => s.SessionKey == sessionKey && s.StatusId == UserSessionStatus.Active).FirstOrDefaultAsync(cancellationToken);
            if (session is not null)
            {
                if (await endSessionAsync(session, UserSessionStatus.LoggedOutByUser, cancellationToken))
                {
                    logger.LogInformation("Session {SessionId} logged out", session.Id);
                }
            }
        }
        public async Task LogoutCookieAsync(string cookieKey, CancellationToken cancellationToken)
        {
            if (TokenService.TryParseRotationKey(cookieKey) is RotationKey rotationKey)
            {
                await LogoutAsync(rotationKey.SessionKey, cancellationToken);
            }
            
        }
        /// <summary>
        /// Marks a session whose refresh lifetime is over as Expired (only if it is still Active). A conditional UPDATE
        /// for the same reason as in <see cref="endSessionAsync"/>: a tracked save could fail on the LatestUpdate token
        /// when the other refresh chain rotates in the same instant, and that exception is not caught anywhere (500).
        /// The phone is left as it is, as before.
        /// </summary>
        private Task markSessionExpiredAsync(UserSession session, CancellationToken cancellationToken) =>
            repository
                .GetQueryable<UserSession>(s => s.Id == session.Id && s.StatusId == UserSessionStatus.Active)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.StatusId, UserSessionStatus.Expired), cancellationToken);

        /// <summary>
        /// Closes the session with the given status; a phone that no other active session uses is marked logged out.
        /// False when the session was no longer Active (someone else ended it first).
        /// </summary>
        /// <remarks>
        /// Conditional UPDATEs rather than a tracked save: a tracked save checks the LatestUpdate concurrency token,
        /// and the other refresh chain's rotation (also a direct UPDATE, which the trigger stamps) could make it fail.
        /// That used to silently skip a replay revocation, or turn a logout into a 500 with the session left active.
        /// The tracked <paramref name="session"/> is left untouched, so no later save writes it back.
        /// </remarks>
        private async Task<bool> endSessionAsync(UserSession session, short statusId, CancellationToken cancellationToken)
        {
            var ended = await repository
                .GetQueryable<UserSession>(s => s.Id == session.Id && s.StatusId == UserSessionStatus.Active)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.StatusId, statusId), cancellationToken) == 1;

            if (ended && session.MobileGpsDeviceId is int deviceId)
            {
                // One statement: the "no other active session uses this phone" check and the update cannot be split
                // by a login on the same phone in between.
                var sessions = repository.DbContext.Set<UserSession>();
                await repository.DbContext.Set<GpsDevice>()
                    .Where(g => g.Id == deviceId && g.StatusId == GpsDeviceStatus.Active
                        && !sessions.Any(s => s.MobileGpsDeviceId == deviceId && s.StatusId == UserSessionStatus.Active))
                    .ExecuteUpdateAsync(setters => setters.SetProperty(g => g.StatusId, GpsDeviceStatus.MobileLogOutInactive), cancellationToken);
            }
            return ended;
        }

        /// <summary>
        /// Issues a password reset key for the Active user the identifier points at and hands it
        /// to <see cref="IPasswordResetKeySender"/>. Exits silently when no user matches, so
        /// callers cannot probe which accounts exist. Keys the user already holds stay valid.
        ///
        /// The call never returns sooner than <see cref="AuthOptions.PasswordResetMinResponseTime"/>
        /// (also when it fails), so how long it took says nothing about whether a user was found,
        /// ignored for the cooldown, or sent a key. This only hides the difference while the real
        /// work finishes inside that time.
        /// </summary>
        public async Task RequestPasswordResetAsync(string? identifier, CancellationToken cancellationToken)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                await IssuePasswordResetKeyForIdentifierAsync(identifier, cancellationToken);
            }
            finally
            {
                var remaining = _authOptions.PasswordResetMinResponseTime - Stopwatch.GetElapsedTime(started);
                if (remaining > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(remaining, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // The caller went away; nobody is left to time the response.
                    }
                }
            }
        }

        private async Task IssuePasswordResetKeyForIdentifierAsync(string? identifier, CancellationToken cancellationToken)
        {
            using var activity = Telemetry.Source.StartActivity("auth.password_reset.request");

            var user = await repository.GetQueryable<User>(u => u.UserName == identifier && u.StatusId == UserStatus.Active && u.UserSourceId != UserSource.AUTO_DeviceUID).AsNoTracking().FirstOrDefaultAsync();
            if (user is null)
            {
                return;
            }
            // A user without an e-mail address still gets a key: delivery is the sender's business
            // (it may reach them another way), and skipping here would make "no e-mail" show up as
            // a difference in what the request did.

            var now = DateTime.UtcNow;
            var lastIssued = await repository
                .GetQueryable<UserPasswordResetKey>(r => r.UserId == user.Id)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => (DateTime?)r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (lastIssued is not null && now - lastIssued < _authOptions.PasswordResetCooldown)
            {
                // Keys no longer replace each other, so this is about volume: it caps the e-mails
                // one account can be sent and the rows one account can accumulate.
                logger.LogInformation("Password reset for user {UserId} ignored: a key was issued within the cooldown", user.Id);
                return;
            }

            // Housekeeping, saved together with the new key.
            var expired = await repository
                .GetQueryable<UserPasswordResetKey>(r => r.UserId == user.Id && r.ExpiresAt <= now)
                .ToListAsync(cancellationToken);
            foreach (var key in expired)
            {
                repository.Delete(key);
            }

            var (resetKey, expiresAt) = await StoreResetKeyAsync(user, _authOptions.PasswordResetKeyLifetime, cancellationToken);

            activity?.SetTag("app.user.id", user.Id);
            await resetKeySender.SendAsync(user, user.Email, resetKey, expiresAt, cancellationToken);
        }

        /// <summary>
        /// Creates a user (status JustCreated, no password) and sends them a key, which doubles
        /// as the activation step: setting a password makes the account Active.
        ///
        /// Uniqueness is only enforced against Active users. The new user stores its unique field
        /// (e-mail in <see cref="EmailUsageMode.UniqueEmail"/> mode, user name otherwise) as
        /// "{guid}_{value}", so the same address can be registered any number of times before
        /// someone finishes activation; the first to do so wins and the rest are cleaned up
        /// (see <see cref="ResetPasswordAsync"/>).
        /// </summary>
        public async Task<RegisterUserResult> RegisterUserAsync(RegisterUserData request, short userSourceId, ClientSideInfo? clientSideInfo, string? clientIp, CancellationToken cancellationToken)
        {
            using var activity = Telemetry.Source.StartActivity("auth.register");
            var errors = new Dictionary<string, string[]>();

            var email = request.Email?.Trim();
            var displayName = request.DisplayName?.Trim();
            var fullName = request.FullName?.Trim();
            var phone = request.Phone?.Trim();
            var userName = request.UserName?.Trim();

            if (_authOptions.EmailMode == EmailUsageMode.UniqueEmail )
            {
                if (string.IsNullOrEmpty(email))
                {
                    errors[nameof(request.Email)] = ["E-mail is required."];
                }
                else if (email.Length > MaxEmailLength || !IsValidEmail(email))
                {
                    errors[nameof(request.Email)] = ["E-mail is not a valid address."];
                }
                else userName = email;

            }
            else
            {
                if (string.IsNullOrEmpty(userName))
                {
                    errors[nameof(request.UserName)] = ["User name is required."];
                }
                else if (userName.Length > MaxNameLength)
                {
                    errors[nameof(request.UserName)] = [$"User name must be at most {MaxNameLength} characters."];
                }
                if (!string.IsNullOrWhiteSpace(email))
                {
                    if (email.Length > MaxEmailLength || !IsValidEmail(email))
                    {
                        errors[nameof(request.Email)] = ["E-mail is not a valid address."];
                    }
                }
            }
            var taken = await repository.GetQueryable<User>(u => u.StatusId == UserStatus.Active && u.UserName == userName).AnyAsync(cancellationToken);
            if (taken)
            {
                if (_authOptions.EmailMode == EmailUsageMode.UniqueEmail) errors[nameof(request.UserName)] = ["A user with this e-mail already exists."];
                else errors[nameof(request.UserName)] = ["A user with this user name already exists."];
            }

            if (string.IsNullOrEmpty(displayName))
            {
                errors[nameof(request.DisplayName)] = ["Display name is required."];
            }
            else if (displayName.Length > MaxNameLength)
            {
                errors[nameof(request.DisplayName)] = [$"Display name must be at most {MaxNameLength} characters."];
            }
            if (fullName is { Length: > MaxNameLength })
            {
                errors[nameof(request.FullName)] = [$"Full name must be at most {MaxNameLength} characters."];
            }
            if (!string.IsNullOrEmpty(phone) && !IsValidPhone(phone))
            {
                errors[nameof(request.Phone)] = ["Phone number may contain only digits, spaces and + - ( ) and must have 5 to 20 characters."];
            }
            if (errors.Count > 0)
            {
                return new RegisterUserResult(RegisterUserStatus.Invalid, errors);
            }


            var pendingPrefix = Guid.NewGuid().ToString("N") + "_";

            // The user and their key are one unit: the key needs the generated user id, so it takes
            // two saves, and without the transaction a failure in between would leave a user who
            // can never be activated. The e-mail goes out only after the commit. The explicit
            // transaction has to run inside the execution strategy (connection retry is enabled),
            // which may re-run the whole block — hence fresh entities on every attempt.
            var db = repository.DbContext;
            User user = null!;
            string resetKey = null!;
            DateTime expiresAt = default;
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                user = new User
                {
                    UserName = pendingPrefix + userName,
                    DisplayName = displayName!,
                    // FullName is required in the database; fall back to the display name.
                    FullName = string.IsNullOrEmpty(fullName) ? displayName! : fullName,
                    // In e-mail mode the e-mail is the unique field and is prefixed like the user
                    // name; otherwise it is plain data.
                    Email = string.IsNullOrWhiteSpace(email) ? null : email,
                    Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                    UserSourceId = userSourceId,
                    StatusId = UserStatus.JustCreated,
                };
                repository.Create(user);
                await repository.SaveAsync(cancellationToken);

                (resetKey, expiresAt) = await StoreResetKeyAsync(user, _authOptions.RegistrationKeyLifetime, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });

            activity?.SetTag("app.user.id", user.Id);
            logger.LogInformation("User {UserId} registered", user.Id);

            await resetKeySender.SendAsync(user, email!, resetKey, expiresAt, cancellationToken);
            return new RegisterUserResult(RegisterUserStatus.Success);
        }

        private const int MaxEmailLength = 254;
        private const int MaxNameLength = 200;

        private static bool IsValidEmail(string email) =>
            System.Net.Mail.MailAddress.TryCreate(email, out var parsed) &&
            parsed.Address == email &&
            parsed.Host.Contains('.');

        private static bool IsValidPhone(string phone) =>
            phone.Length is >= 5 and <= 20 && phone.All(c => char.IsAsciiDigit(c) || c is '+' or ' ' or '-' or '(' or ')');

        // "{32 hex chars}_": see RegisterUserAsync.
        private const int PendingPrefixLength = 33;

        /// <summary>Splits "{guid}_{value}" into value; false (value = stored) when there is no such prefix.</summary>
        private static bool TryStripPendingPrefix(string stored, out string value)
        {
            if (stored.Length > PendingPrefixLength &&
                stored[PendingPrefixLength - 1] == '_' &&
                stored.AsSpan(0, PendingPrefixLength - 1).IndexOfAnyExcept("0123456789abcdef") < 0)
            {
                value = stored[PendingPrefixLength..];
                return true;
            }
            value = stored;
            return false;
        }

        /// <summary>
        /// Generates a key for the user and saves its hash (together with anything else already
        /// pending on the context). Returns the plain key for delivery.
        /// </summary>
        private async Task<(string Key, DateTime ExpiresAt)> StoreResetKeyAsync(
            User user, TimeSpan lifetime, CancellationToken cancellationToken)
        {
            // "{userId}.{secret}": the id lets the reset endpoint find the user from the key
            // alone. It is not a secret (and not trusted) — the whole string is what is hashed
            // and compared. Base64url never contains '.', so the split is unambiguous.
            var resetKey = $"{user.Id}.{TokenService.GenerateKey()}";
            var now = DateTime.UtcNow;
            var expiresAt = now + lifetime;

            repository.Create(new UserPasswordResetKey
            {
                KeyHash = HashResetKey(resetKey),
                UserId = user.Id,
                CreatedAt = now,
                ExpiresAt = expiresAt,
            });

            await repository.SaveAsync(cancellationToken);
            return (resetKey, expiresAt);
        }

        /// <summary>
        /// Sets a new password when the reset key matches and has not expired. The password is
        /// stored as an Argon2id hash and the user's other sessions are closed. Then, by status:
        /// <list type="bullet">
        /// <item>Active: all of the user's reset keys are removed.</item>
        /// <item>JustCreated: the user's unique field loses its "{guid}_" prefix and the account
        /// becomes Active; every other JustCreated registration of the same e-mail / user name is
        /// deleted along with the keys of all of them.</item>
        /// </list>
        /// </summary>
        public async Task<ResetPasswordResult> ResetPasswordAsync(
            string? resetKey, string newPassword, CancellationToken cancellationToken)
        {
            using var activity = Telemetry.Source.StartActivity("auth.password_reset.complete");

            var invalid = new ResetPasswordResult(ResetPasswordStatus.InvalidResetKey);

            // The user is identified by the id embedded in the key; the key itself is what proves it.
            var separator = resetKey?.IndexOf('.') ?? -1;
            if (separator <= 0 || !int.TryParse(resetKey![..separator], out var userId))
            {
                return invalid;
            }

            var keyHash = HashResetKey(resetKey);
            var now = DateTime.UtcNow;
            var record = await repository
                .GetQueryable<UserPasswordResetKey>(r => r.UserId == userId && r.KeyHash == keyHash)
                .FirstOrDefaultAsync(cancellationToken);
            if (record is null)
            {
                logger.LogWarning("Password reset rejected for user {UserId}: invalid reset key", userId);
                return invalid;
            }

            // Expiry is checked before anything else happens with the key.
            if (record.ExpiresAt <= now)
            {
                repository.Delete(record);
                await repository.SaveAsync(cancellationToken);
                logger.LogInformation("Password reset rejected for user {UserId}: reset key expired", userId);
                return invalid;
            }

            var user = await repository
                .GetQueryable<User>(u => u.Id == userId &&
                    (u.StatusId == UserStatus.Active || u.StatusId == UserStatus.JustCreated) &&
                    u.UserSourceId != UserSource.AUTO_DeviceUID)
                .FirstOrDefaultAsync(cancellationToken);
            if (user is null)
            {
                // Deactivated since the key was issued.
                logger.LogWarning("Password reset rejected for user {UserId}: user not eligible", userId);
                return invalid;
            }

            // What the user would recognise as their own name/e-mail, without the pending prefix.
            var hasPrefixedUserName = TryStripPendingPrefix(user.UserName, out var userName);
            var hasPrefixedEmail = false;
            var email = user.Email;
            if (email is not null)
            {
                hasPrefixedEmail = TryStripPendingPrefix(email, out email);
            }

            // Checked only once the key is proven, so the rules are not exposed to strangers;
            // a rejected password leaves the key in place for another attempt.
            var passwordErrors = PasswordPolicy.Validate(newPassword, userName, email?.Split('@')[0]);
            if (passwordErrors.Count > 0)
            {
                return new ResetPasswordResult(ResetPasswordStatus.PasswordRejected, passwordErrors);
            }

            var pendingUsersToRemove = new List<User>();
            if (user.StatusId == UserStatus.JustCreated)
            {
                var emailMode = _authOptions.EmailMode == EmailUsageMode.UniqueEmail;
                // Only accounts that went through registration carry a prefix; an older
                // JustCreated user without one is simply activated.
                if (emailMode ? hasPrefixedEmail : hasPrefixedUserName)
                {
                    var uniqueValue = emailMode ? email! : userName;
                    if (await IsTakenByActiveUserAsync(user.Id, uniqueValue, emailMode, cancellationToken))
                    {
                        // Someone else finished activating (or was created) with this name after
                        // this registration passed its own check.
                        logger.LogInformation("Activation of user {UserId} rejected: unique field taken by an Active user", user.Id);
                        return new ResetPasswordResult(ResetPasswordStatus.IdentifierTaken);
                    }
                    pendingUsersToRemove = await FindOtherPendingRegistrationsAsync(user.Id, uniqueValue, emailMode, cancellationToken);
                }

                if (hasPrefixedUserName)
                {
                    user.UserName = userName;
                }
                if (hasPrefixedEmail)
                {
                    user.Email = email;
                }
                user.StatusId = UserStatus.Active;
            }

            user.Password = passwordHasher.Hash(newPassword);

            // Every key of this user goes (the one just used included); so do the other pending
            // registrations of the same address, with all of their keys.
            var removedUserIds = pendingUsersToRemove.Select(u => u.Id).Append(user.Id).ToList();
            var keysToRemove = await repository
                .GetQueryable<UserPasswordResetKey>(r => removedUserIds.Contains(r.UserId))
                .ToListAsync(cancellationToken);
            foreach (var key in keysToRemove)
            {
                repository.Delete(key);
            }
            foreach (var pendingUser in pendingUsersToRemove)
            {
                repository.Delete(pendingUser);
            }

            // Whoever held a session before may be the reason the password is being reset: end
            // them all, so the only way back in is the new password. Same save as the password
            // change — either both happen or neither. (Access tokens already issued live on
            // until their short expiry; they cannot be recalled.)
            var sessions = await repository
                .GetQueryable<UserSession>(s => s.UserId == user.Id && s.StatusId == UserSessionStatus.Active)
                .ToListAsync(cancellationToken);
            foreach (var session in sessions)
            {
                session.StatusId = UserSessionStatus.LoggedOutByUser;
            }
            await repository.SaveAsync(cancellationToken);

            // Whoever proved ownership with the key must not stay locked out by earlier failed logins.
            loginThrottle.Reset(ThrottleKey(user.UserName));

            activity?.SetTag("app.user.id", user.Id);
            logger.LogInformation(
                "Password set for user {UserId}: {KeyCount} key(s) removed, {SessionCount} session(s) closed, {DuplicateCount} duplicate registration(s) removed",
                user.Id, keysToRemove.Count, sessions.Count, pendingUsersToRemove.Count);
            return new ResetPasswordResult(ResetPasswordStatus.Success);
        }

        private Task<bool> IsTakenByActiveUserAsync(int excludeUserId, string value, bool byEmail, CancellationToken cancellationToken)
        {
            if (byEmail)
            {
                var lower = value.ToLowerInvariant();
                return repository
                    .GetQueryable<User>(u => u.Id != excludeUserId && u.StatusId == UserStatus.Active &&
                        ((u.Email != null && u.Email.ToLower() == lower) || u.UserName == lower))
                    .AnyAsync(cancellationToken);
            }
            return repository
                .GetQueryable<User>(u => u.Id != excludeUserId && u.StatusId == UserStatus.Active && u.UserName == value)
                .AnyAsync(cancellationToken);
        }

        /// <summary>
        /// Other JustCreated registrations whose unique field is exactly "{guid}_{value}". The
        /// SQL narrows by suffix; the exact check runs in memory because a suffix alone also matches
        /// longer names that merely end the same way ("alice_bob" ends with "_bob").
        /// </summary>
        private async Task<List<User>> FindOtherPendingRegistrationsAsync(
            int excludeUserId, string value, bool byEmail, CancellationToken cancellationToken)
        {
            var query = repository.GetQueryable<User>(u =>
                u.Id != excludeUserId && u.StatusId == UserStatus.JustCreated && (u.UserSourceId == UserSource.Registration_Portal || u.UserSourceId == UserSource.Registration_App));

            var lowerSuffix = "_" + value.ToLowerInvariant();
            List<User> candidates;
            if (byEmail)
            {
                candidates = await query
                    .Where(u => u.Email != null && u.Email.ToLower().EndsWith(lowerSuffix))
                    .ToListAsync(cancellationToken);
                return candidates
                    .Where(u => TryStripPendingPrefix(u.Email!, out var v) && string.Equals(v, value, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // UserName's own collation is non-deterministic, which PostgreSQL before 18 rejects in LIKE,
            // so the suffix match runs under the database's default (deterministic) collation.
            candidates = await query
                .Where(u => EF.Functions.Collate(u.UserName, "default").ToLower().EndsWith(lowerSuffix))
                .ToListAsync(cancellationToken);
            return candidates
                .Where(u => TryStripPendingPrefix(u.UserName, out var v) && string.Equals(v, value, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static string HashResetKey(string resetKey) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(resetKey)));
    }
}
