# Refresh-session hardening — implementation plan

Scope: three findings from the auth review of `api-server`.

1. The refresh cookie identifies the session by the rotation key alone: a replayed stolen key is never acted on, keys are stored in plaintext, and every refresh scans `user_session`.
2. The refresh cookie's `Secure` flag depends on `Request.IsHttps`.
3. Two concurrent refreshes with the same key make one of them fail with a 5xx.

Out of scope: `/api/auth_auto/*` device-UID authentication (separate work), access-token revocation.

## Decisions to confirm with the owner before coding

| # | Question | Default assumed below |
|---|----------|-----------------------|
| D1 | Existing portal/app login sessions: force one re-login at deploy, or migrate them lazily? | Force re-login. Auto-device sessions are unaffected. |
| D2 | Where does the reverse proxy run relative to the API (same host, or which IPs / networks)? | Unknown — the dev must ask; do not guess addresses. |
| D3 | The key-derivation secret: derive from `Jwt:SigningKey`, or a new required config value? | Derive from `Jwt:SigningKey` (no deployment change). |

## Facts the plan relies on (verified in code)

- `UserSession` and `SessionTokenInfo` are **not in this repo**. They live in `../server-side-commons/exs.modelCommons/UserManagement/`, and the table is mapped in `../server-side-commons/exs.dbContextCommons/CommonModelBuilder.cs`. Other solutions (`server-side-notifications`, `server-side-sp-api-server`) reference the same types, so a model change is cross-repo. Read the README sections on "hand-emptied" migrations before generating any migration.
- `user_session` has no index on either rotation-key column (`CommonModelBuilder.cs:19-26`).
- `UserSession.LatestUpdate` is an optimistic-concurrency token stamped by a DB trigger (`BaseDatabaseContext.cs:19-28`). A concurrent second save therefore throws `DbUpdateConcurrencyException`, which nothing in `AuthService` catches.
- Session ids are sequential ints and are exposed (`sid` claim, `UserSessionCheckResponse.SessionId`). They must never be treated as proof of anything.
- Integration tests run in environment `Testing` over plain HTTP (`tests/api-server.IntegrationTests/ApiFixture.cs`), and `AuthFlowTests.cs` parses the cookie value and backdates `Token.LatestRefreshTime`.
- The duplicate-refresh ("grace") path must keep returning the **current** key to a caller that presents the just-superseded one (`AuthFlowTests.Refresh_WithImmediatelyReplayedKey_IsToleratedAsDuplicate`). This rules out "store only a hash of a random key", because the server could not re-send the current key.

## Part 1 — Session-bound, derived rotation keys

### Design

The cookie value becomes `{sessionId}.{counter}.{mac}`:

- `sessionId` — `UserSession.Id`.
- `counter` — a per-session integer, incremented on every rotation.
- `mac` — base64url of `HMAC-SHA256(refreshKey, "rt1|{sessionId}|{counter}|{salt}")`.
- `salt` — 32 random bytes generated at login, stored on the session (base64url text). It is useless without `refreshKey`.
- `refreshKey` — `HMAC-SHA256(key: UTF8(Jwt:SigningKey), data: "refresh-rotation-key-v1")`, computed once in `TokenService` (D3).

Nothing that can be replayed is stored in the database, and the server can recompute any key of a session, which keeps the grace path working.

### Validation algorithm (replaces key lookups in `RefreshLoginAsync`)

1. Parse the cookie into three parts; `sessionId` and `counter` must parse as non-negative ints. Anything else: 401 and clear the cookie.
2. Recompute the MAC from the **presented** `sessionId` and `counter` and the session's stored salt; compare with `CryptographicOperations.FixedTimeEquals`. To get the salt, load the session by primary key (any status).
   - Session not found, or MAC mismatch: 401, clear cookie, **do not touch the session** (a guessed id must not be able to log anyone out).
3. MAC is valid, session not `Active`: 401, clear cookie.
4. MAC is valid, session `Active`, compare `counter` with `Token.RotationCounter`:
   - equal: normal path — user-active check, then rotate (Part 3).
   - `current - 1` and `LatestRefreshTime` within `RefreshReuseGraceWindow` and session not expired: duplicate path — return success with the key for the **current** counter, no rotation.
   - any other value (lower, `current - 1` outside the grace window, or greater than current): **revoke the session** (below), 401, clear cookie. A greater counter means the database lost rotations (restore or failover), so an older, possibly stolen key has become current again; the caller is logged out either way, and revoking closes that window.

### Revocation on reuse

- Add `UserSessionStatus.RevokedKeyReuse = 5` in `server-side-commons` (`refs/UserSessionStatus.cs`). Check whether a `user_session_status` reference table/seed exists in any consuming repo and add the row there.
- Extract the session-ending code from `LogoutAsync` (`AuthService.cs:389-404`, including the `MobileGpsDeviceId` → `MobileLogOutInactive` handling) into one private method taking the target status; use it for logout and for reuse revocation.
- Log at Warning with session id and user id; add a counter in `core/Telemetry.cs` next to `LoginFailures`.

### Model and migration (`server-side-commons`, then this repo)

- `SessionTokenInfo`: add `int RotationCounter` and `string KeySalt` (default `string.Empty`); remove `SessionRotationKey` and `PreviousSessionRotationKey`.
- Find every use of the removed properties across all four repos (`server-side-commons`, `server-side-notifications`, `server-side-sp-api-server`, this one) before deleting them; `server-side-sp-api-server` has its own session code and may need the columns — if so, stop and report instead of removing.
- Generate the migration in whichever context owns `user_session` for this database, following the README convention for shared tables. The migration: add the two columns, set every `Active` session whose `SessionSourceId` is `Login_Portal` (2) or `Login_App` (3) to `Expired` (D1), drop the two key columns.
- Auto-device sessions (`SessionSourceId = 1`) never use the cookie; leave them `Active`. `RotateAsync` must still work for them with an empty salt (it only increments the counter).

### Code changes in this repo

- `TokenService`: add `CreateRotationKey(int sessionId, int counter, string salt)` and `TryParseRotationKey(string, out int sessionId, out int counter, out byte[] mac)`; keep `GenerateKey()` for the salt and reset keys. Fix the stale comment at `TokenService.cs:66-67`.
- `AuthService.LoginAsync`: set `KeySalt = TokenService.GenerateKey()`, `RotationCounter = 0`; drop `SessionRotationKey`.
- `AuthService.BuildSuccessAsync`: build the cookie value from `session.Id`, `Token.RotationCounter`, `Token.KeySalt`.
- `AuthService.RefreshLoginAsync`: implement the algorithm above; delete `FindActiveSessionAsync` and `FindRecentlySupersededSessionAsync`.
- `AuthService.LogoutAsync`: keep the `sid`-from-access-token path first. Cookie fallback: parse, load by id, accept **any** valid MAC for that session regardless of counter (ending a session is the safe direction).
- `Program.cs:216-217`: remove the stale "refresh tokens are signed with the same key" comment or reword it.
- README "Auth" section: describe the new cookie format and the reuse-revocation behaviour.

## Part 2 — `Secure` cookie and proxy trust

- `JwtOptions`: add `bool AllowInsecureRefreshCookie` (default `false`).
- `TokenService.BuildRefreshCookieOptions`: `Secure = isHttps || !AllowInsecureRefreshCookie`. The cookie is then always `Secure` unless a developer opts out.
- Set `Jwt:AllowInsecureRefreshCookie = true` only in `api-server/local_configs/local_server.json` (dev over http) and in the test fixture's config. Never in `appsettings.json` or `configs/`.
- `AuthController.ClearRefreshCookie`: build the options through the same `TokenService` method so the delete carries matching attributes.
- `Program.cs:180-183` (D2): read `ForwardedHeaders:KnownProxies` and `ForwardedHeaders:KnownNetworks` (string arrays) from configuration and add them to `ForwardedHeadersOptions`. On .NET 10 check for the obsolete warning on `KnownNetworks` and use the replacement property if there is one. Without this, behind a non-loopback proxy `Request.IsHttps` is false and every client shares one rate-limit partition (`RemoteIpAddress` is the proxy).
- Log one Warning at startup when the environment is not Development and `AllowInsecureRefreshCookie` is true.

## Part 3 — Concurrent refresh

- In `RotateAsync`: increment `Token.RotationCounter`, set `LatestRefreshTime`, save. Wrap the save in `try/catch (DbUpdateConcurrencyException)`.
- On that exception: detach/reload the session from the database (`repository.DbContext`, as `RegisterUserAsync` already does), then re-run the counter comparison from Part 1 step 4 with the reloaded row. The loser of the race now sees `presented == current - 1` inside the grace window and gets the winner's key.
- Do not retry the rotation itself; one reload and re-evaluation only.
- `RefreshAutoDeviceAsync` shares `RotateAsync`: on the same exception, reload and return success built from the reloaded session (it has no cookie to reconcile).
- Verify that the `LatestUpdate` trigger exists on `user_session` in the test database; the fix depends on it. If it does not, stop and report.

## Tests (`tests/api-server.IntegrationTests/AuthFlowTests.cs`)

Update the existing helpers (`ExtractRotationKey`, `RefreshRequestWithKey`, `BackdateLatestRefreshTimeAsync`) for the new format, then add or adjust:

1. Refresh rotates: counter in the cookie goes up by exactly one; value differs.
2. Immediate replay of the previous key returns the same current key and does not rotate (existing test, adjusted).
3. Replay of the previous key after the grace window: 401, **and** a following refresh with the current key is also 401, and the session row has status `RevokedKeyReuse`.
4. Replay of a key two rotations old, inside the grace window of the latest rotation: same outcome as 3.
4a. A key with a valid MAC and a counter above the stored one (lower `Token.RotationCounter` directly in the DB to simulate a restore): 401, session status `RevokedKeyReuse`.
5. Valid session id with a forged MAC: 401, session still `Active`, the real key still refreshes.
6. Malformed cookie values (no dots, non-numeric parts, empty): 401, no exception.
7. Two refreshes fired in parallel with the same key (cookie handling off, `Task.WhenAll`): both 200, both carry the same cookie value, counter advanced by one. Repeat the pair several times in the test to make the race likely.
8. `Set-Cookie` contains `secure` when `AllowInsecureRefreshCookie` is false (separate fixture or config override).
9. Logout with a cookie whose counter is stale but MAC valid ends the session.
10. Reuse revocation of a session with `MobileGpsDeviceId` sets the device to `MobileLogOutInactive` when no other active session uses it.

Run the whole integration suite afterwards; `PasswordResetTests`, `NotificationServiceTests` and `AutoDeviceGpsDeviceTests` construct or query `UserSession` and must still pass.

## Order of work

1. Confirm D1–D3.
2. `server-side-commons`: model + status constant. Build all consuming solutions.
3. This repo: `TokenService`, `AuthService`, `AuthController`, migration.
4. Part 3 (it builds on the counter from Part 1).
5. Part 2.
6. Tests, README.

## Done when

- No column in `user_session` holds a value that can be replayed as a refresh credential.
- Refresh and logout never query `user_session` by anything other than primary key or `UserId`.
- Tests 1–10 pass and the full integration suite is green.
- A production-like configuration issues the cookie with `Secure` regardless of `Request.IsHttps`.
- Deployment notes list: users with portal/app logins sign in once after deploy; `ForwardedHeaders:*` values to set per environment.
