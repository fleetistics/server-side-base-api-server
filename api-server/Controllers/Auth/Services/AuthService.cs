using api_server.Auth.Dto;
using api_server.Controllers.Teams.Services;
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
    public enum AuthStatus
    {
        Success,
        InvalidCredentials,
        Unauthenticated,
        /// <summary>The account is temporarily locked after repeated failed logins.</summary>
        TooManyAttempts,
    }

    /// <param name="RotationKey">New rotation key the caller should write to the cookie.</param>
    /// <param name="ClearCookie">True when the caller should delete the refresh cookie.</param>
    /// <param name="RetryAfter">How long the lock lasts (TooManyAttempts only).</param>
    public sealed record AuthResult(
        AuthStatus Status,
        UserSessionCheckResponse? Response = null,
        string? RotationKey = null,
        bool ClearCookie = false,
        TimeSpan? RetryAfter = null)
    {
        public bool Succeeded => Status == AuthStatus.Success;
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
        ITeamService teamService,
        IOptions<AuthOptions> authOptions,
        PasswordHasher passwordHasher,
        IPasswordResetKeySender resetKeySender,
        LoginThrottle loginThrottle,
        ILogger<AuthService> logger)
    {
        private readonly AuthOptions _authOptions = authOptions.Value;

        /// <param name="clientIp">Only for the log; null when unknown.</param>
        public async Task<AuthResult> LoginAsync(string userName, string password, string? clientIp, CancellationToken cancellationToken)
        {
            using var activity = Telemetry.Source.StartActivity("auth.login");

            var name = userName.Trim();
            var throttleKey = ThrottleKey(name);
            clientIp ??= "unknown";

            // A locked account refuses everything — the right password included — before any
            // lookup or hashing. The lock is per typed name, existing or not, so refusing early
            // says nothing about whether the account exists.
            var retryAfter = loginThrottle.GetRetryAfter(throttleKey);
            if (retryAfter is not null)
            {
                Telemetry.LoginFailures.Add(1);
                activity?.SetStatus(ActivityStatusCode.Error, "account locked");
                logger.LogWarning("Login refused for {UserName} from {ClientIp}: account locked for another {RetryAfterSeconds} s",
                    ForLog(name), clientIp, (int)Math.Ceiling(retryAfter.Value.TotalSeconds));
                return new AuthResult(AuthStatus.TooManyAttempts, RetryAfter: retryAfter);
            }

            // Only Active users can log in, and only Active users hold a name (an Inactive account
            // may share its user name with a newer Active one), so the status is part of the lookup.
            // In unique-e-mail mode the user name is an e-mail address, which registration and
            // password reset already treat case-insensitively; login does too.
            var activeUsers = repository.GetQueryable<User>(u => u.StatusId == UserStatus.Active);
            User? user;
            if (_authOptions.EmailMode == EmailUsageMode.UniqueEmail)
            {
                var lowerName = name.ToLowerInvariant();
                user = await activeUsers.Where(u => u.UserName.ToLower() == lowerName).FirstOrDefaultAsync(cancellationToken);
            }
            else
            {
                user = await activeUsers.Where(u => u.UserName == name).FirstOrDefaultAsync(cancellationToken);
            }

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
                return new AuthResult(AuthStatus.InvalidCredentials);
            }

            loginThrottle.Reset(throttleKey);
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

            return await BuildSuccessAsync(session, cancellationToken);
        }

        // What the failure counter is keyed by: the typed name, compared the way login compares it
        // (case-insensitively for e-mail user names). Very long input is hashed so a key stays small.
        private string ThrottleKey(string name)
        {
            var key = _authOptions.EmailMode == EmailUsageMode.UniqueEmail ? name.ToLowerInvariant() : name;
            return key.Length <= 256 ? key : "h:" + HashResetKey(key);
        }

        // The typed name goes into the log: strip control characters (no forged log lines) and
        // cap the length (the field is attacker-controlled).
        private static string ForLog(string value)
        {
            var clean = string.Concat(value.Where(c => !char.IsControl(c)));
            return clean.Length > 100 ? clean[..100] + "…" : clean;
        }

        /// <summary>Rotates the session and issues a new access token.</summary>

        private Task convertClientSideInfoToSessionClientInfo(ClientSideInfo clientSideInfo, SessionClientInfo sessionClientInfo)
        {
            sessionClientInfo.DeviceUID = clientSideInfo.DeviceUID;
            sessionClientInfo.FCM_FID = clientSideInfo.FCM_FID;
            sessionClientInfo.AppVersion = clientSideInfo.AppVersion;
            sessionClientInfo.CodeVersion = clientSideInfo.CodeVersion;
            sessionClientInfo.PlatformId = clientSideInfo.PlatformId;
            //switch (clientSideInfo.PlatformName.ToLower())
            //{
            //    case "ios":
            //        sessionClientInfo.PlatformId = ClientDevicePlatform.Ios;
            //        break;
            //    case "android":
            //        sessionClientInfo.PlatformId = ClientDevicePlatform.Android;
            //        break;
            //    case "windows":
            //        sessionClientInfo.PlatformId = ClientDevicePlatform.Windows;
            //        break;
            //    default:
            //        sessionClientInfo.PlatformId = ClientDevicePlatform.Unknown;
            //        break;
            //}
            return Task.CompletedTask;
        }
        private void refreshSessionClientInfo(ClientSideInfo clientSideInfo, SessionClientInfo sessionClientInfo)
        {
            sessionClientInfo.FCM_FID = clientSideInfo.FCM_FID;
            sessionClientInfo.AppVersion = clientSideInfo.AppVersion;
            sessionClientInfo.CodeVersion = clientSideInfo.CodeVersion;
        }
        // Phones are always provider Mobile; the platform decides the device type (null when unknown).
        private static int? convertClientSideInfoToGpsDeviceTypeId(SessionClientInfo sessionClientInfo)
        {
            switch (sessionClientInfo.PlatformId)
            {
                case ClientDevicePlatform.Ios:
                    return GpsDeviceType.MobileIos;
                case ClientDevicePlatform.Android:
                    return GpsDeviceType.MobileAndroid;
                default:
                    return null;
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
                var gpsDevice = await repository.GetQueryable<GpsDevice>(e => e.ProviderId == GpsDeviceProvider.Mobile && e.SerialNumber == clientSideInfo.DeviceUID).FirstOrDefaultAsync(cancellationToken);
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
                    gpsDevice = new GpsDevice
                    {
                        SerialNumber = clientSideInfo.DeviceUID,
                        UserId = user.Id,
                        ProviderId = GpsDeviceProvider.Mobile,
                        TypeId = convertClientSideInfoToGpsDeviceTypeId(session.ClientInfo),
                        StatusId = GpsDeviceStatus.Active
                    };
                    repository.Create(gpsDevice);
                    await repository.SaveAsync(cancellationToken);
                    session.MobileGpsDeviceId = gpsDevice.Id;
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

                //hack to keep the session alive for auto device users, since they don't have a refresh token, we will extend the expiration time every time they refresh the session
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
                return await BuildSuccessAsync(superseded, cancellationToken);
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

            return await BuildSuccessAsync(session, cancellationToken);
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

        private async Task<AuthResult> BuildSuccessAsync(UserSession session, CancellationToken cancellationToken)
        {
            // Looked up on every token issue/renewal so the claim follows team changes;
            // no team -> no claim (a null is never written into the JWT).
            var teamId = await teamService.GetUserActiveTeamIdAsync(session.UserId, cancellationToken);
            return new(AuthStatus.Success,
                new UserSessionCheckResponse
                {
                    AccessToken = tokenService.CreateAccessToken(session.UserId.ToString(), session.Id.ToString(), session.MobileGpsDeviceId?.ToString(), teamId: teamId?.ToString()),
                    UserId = session.UserId,
                    SessionId = session.Id,
                },
                session.Token.SessionRotationKey);
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

            var user = await FindUserForPasswordResetAsync(identifier, cancellationToken);
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
        public async Task<RegisterUserResult> RegisterUserAsync(RegisterUserData request, CancellationToken cancellationToken)
        {
            using var activity = Telemetry.Source.StartActivity("auth.register");

            var emailMode = _authOptions.EmailMode == EmailUsageMode.UniqueEmail;
            var email = request.Email?.Trim();
            var displayName = request.DisplayName?.Trim();
            var fullName = request.FullName?.Trim();
            var phone = request.Phone?.Trim();
            // In unique-email mode the e-mail is the login name and any supplied user name is ignored.
            var userName = emailMode ? email : request.UserName?.Trim();

            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrEmpty(email))
            {
                errors[nameof(request.Email)] = ["E-mail is required."];
            }
            else if (email.Length > MaxEmailLength || !IsValidEmail(email))
            {
                errors[nameof(request.Email)] = ["E-mail is not a valid address."];
            }
            if (string.IsNullOrEmpty(displayName))
            {
                errors[nameof(request.DisplayName)] = ["Display name is required."];
            }
            else if (displayName.Length > MaxNameLength)
            {
                errors[nameof(request.DisplayName)] = [$"Display name must be at most {MaxNameLength} characters."];
            }
            if (!emailMode)
            {
                if (string.IsNullOrEmpty(userName))
                {
                    errors[nameof(request.UserName)] = ["User name is required."];
                }
                else if (userName.Length > MaxNameLength)
                {
                    errors[nameof(request.UserName)] = [$"User name must be at most {MaxNameLength} characters."];
                }
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

            // Only Active users hold a name: pending registrations and deactivated accounts don't count.
            if (emailMode)
            {
                var lowerEmail = email!.ToLowerInvariant();
                var taken = await repository
                    .GetQueryable<User>(u => u.StatusId == UserStatus.Active &&
                        ((u.Email != null && u.Email.ToLower() == lowerEmail) || u.UserName.ToLower() == lowerEmail))
                    .AnyAsync(cancellationToken);
                if (taken)
                {
                    return new RegisterUserResult(RegisterUserStatus.Conflict,
                        new Dictionary<string, string[]> { [nameof(request.Email)] = ["A user with this e-mail already exists."] });
                }
            }
            else
            {
                var taken = await repository
                    .GetQueryable<User>(u => u.StatusId == UserStatus.Active && u.UserName == userName)
                    .AnyAsync(cancellationToken);
                if (taken)
                {
                    return new RegisterUserResult(RegisterUserStatus.Conflict,
                        new Dictionary<string, string[]> { [nameof(request.UserName)] = ["A user with this user name already exists."] });
                }
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
                    Email = emailMode ? pendingPrefix + email : email,
                    Phone = string.IsNullOrEmpty(phone) ? null : phone,
                    UserSourceId = UserSource.Registered,
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
                        ((u.Email != null && u.Email.ToLower() == lower) || u.UserName.ToLower() == lower))
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
                u.Id != excludeUserId && u.StatusId == UserStatus.JustCreated && u.UserSourceId == UserSource.Registered);

            List<User> candidates;
            if (byEmail)
            {
                var lowerSuffix = "_" + value.ToLowerInvariant();
                candidates = await query
                    .Where(u => u.Email != null && u.Email.ToLower().EndsWith(lowerSuffix))
                    .ToListAsync(cancellationToken);
                return candidates
                    .Where(u => TryStripPendingPrefix(u.Email!, out var v) && string.Equals(v, value, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var suffix = "_" + value;
            candidates = await query
                .Where(u => u.UserName.EndsWith(suffix))
                .ToListAsync(cancellationToken);
            return candidates
                .Where(u => TryStripPendingPrefix(u.UserName, out var v) && v == value)
                .ToList();
        }

        // What the identifier means depends on Auth:EmailMode: an e-mail address when e-mails are
        // unique, otherwise a user name (the only thing then trusted to be unique). Only Active
        // users are reachable this way — a pending registration's fields carry a prefix, so it
        // cannot match, and its owner simply registers again. Anything ambiguous or missing
        // resolves to "no user".
        private async Task<User?> FindUserForPasswordResetAsync(string? identifier, CancellationToken cancellationToken)
        {
            var normalized = identifier?.Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            var query = repository.GetQueryable<User>(u =>
                u.StatusId == UserStatus.Active && u.UserSourceId != UserSource.AUTO_DeviceUID);

            List<User> matches;
            if (_authOptions.EmailMode == EmailUsageMode.UniqueEmail)
            {
                var normalizedEmail = normalized.ToLowerInvariant();
                matches = await query
                    .Where(u => u.Email != null && u.Email.ToLower() == normalizedEmail)
                    .Take(2)
                    .ToListAsync(cancellationToken);
            }
            else
            {
                matches = await query
                    .Where(u => u.UserName == normalized)
                    .Take(2)
                    .ToListAsync(cancellationToken);
            }

            if (matches.Count > 1)
            {
                logger.LogWarning("Password reset skipped: identifier matches several users although {Mode} was configured", _authOptions.EmailMode);
                return null;
            }
            return matches.SingleOrDefault();
        }

        private static string HashResetKey(string resetKey) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(resetKey)));
    }
}
