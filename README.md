# MF Ai Console API

ASP.NET Core (.NET 10) backend for the Ai Console SPA. Also serves as the base
project for future backends — the patterns below are the template.

## Solution layout

| Project | Role |
|---|---|
| `api-server` | Web API host: controllers, auth, telemetry, DI |
| `main-database` | EF Core `MainDatabaseContext` + Npgsql wiring |
| `db-model` | Entity classes (no EF dependencies) |
| `exs.commons`, `exs.databaseCommons`, `exs.webCommons` | Shared infrastructure (JSON config, `IRepository` implementation) |
| `tests/api-server.IntegrationTests` | xUnit + WebApplicationFactory + Testcontainers (real PostgreSQL) |
| `telemetry/` | `docker-compose.lgtm.yml` — local Grafana LGTM observability backend |
| `fcm-notification-sender` | Standalone Worker Service (systemd), drains `notification_queue` and pushes via FCM |

## Prerequisites

- .NET 10 SDK
- Docker Desktop (integration tests and the LGTM backend run in containers)
- PostgreSQL database (connection string below)

## First run

1. Configure `api-server/local_configs/local_server.json` — it holds the dev
   Kestrel URL (`http://localhost:5199`), `ConnectionStrings:MainDatabase`,
   `Jwt:SigningKey`, `Media` upload folder, Serilog sinks, and the OTLP endpoint.
2. ```bash
   dotnet run --project api-server
   ```
3. Swagger/OpenAPI (Development only): `http://localhost:5199/openapi/v1.json`

> **Config gotcha:** `local_configs/` is loaded from the *build output*
> (`bin/Debug/net10.0/local_configs`), copied at build time. If you edit a config
> and run with `--no-build`, your change is not live — rebuild first.

## Tests

```bash
dotnet test
```

Integration tests boot the full app against a disposable PostgreSQL container
(`postgis/postgis:16`) via Testcontainers, reset data between tests with Respawn,
and log in through the real auth endpoints. Docker must be running. See
`tests/api-server.IntegrationTests/ApiFixture.cs` for the environment knobs the
tests set (JWT key, media folder, rate-limit override).

## Architecture notes (the template patterns)

**Auth** — JWT bearer access tokens (in SPA memory) + rotating refresh key in an
httpOnly cookie scoped to `/api/auth`. Rotation is single-use: replaying an old
key fails and clears the cookie. `AuthAPIController` base class = `[Authorize]`
plus `UserId`/`SessionId` claims. Role policies live in `core/AuthPolicies.cs`.
Login is rate-limited (fixed window per IP, `RateLimiting:Login:PermitLimit`,
default 5/min).

**Controllers stay thin** — logic lives in services injected via interfaces
(`AuthService`, `IMediaStorage`/`MediaStorageService`). Controllers translate
HTTP ⇄ service results. Follow this split for new endpoints.

**Errors** — RFC 7807 ProblemDetails everywhere: a global `IExceptionHandler`
(`core/GlobalExceptionHandler.cs`) catches anything unhandled, logs it, and
returns a problem response carrying the `traceId` for lookup in the telemetry
backend. Exception details are only included in Development.

**Idempotency** — `[Idempotent]` on unsafe POSTs requires an `Idempotency-Key`
header (GUID); duplicate keys return the cached response. Store is in-memory —
**single-instance only**; swap for a distributed store before load-balancing.

**Background work** — `ClientLogRetentionService` (daily purge of uploaded client
logs, `ClientLog:RetentionDays`, default 90) is the pattern: a `BackgroundService`
creating a DI scope per run.

**Notifications** — transport-agnostic fan-out, transport-specific delivery.
`INotifier`/`NotificationService` (`api-server`) buffer requests in memory, then
persist one `Notification` + one `NotificationToUser` per user + one
`NotificationQueue` row per (user, transport); today that's always FCM. The
standalone `fcm-notification-sender` project (a separate systemd-hosted Worker
Service, see its own directory) polls `notification_queue`, resolves each user's
currently-active mobile `UserSession`s, sends via FirebaseAdmin with an in-memory
Polly retry (transient vs. permanent `FirebaseMessagingException` codes), then
writes the terminal `NotificationSent` (+ one `FCMNotificationSent` per session
attempted) and removes the queue row. Adding email/SMS/portal later means another
`NotificationQueue`-TransportType and another standalone sender — `NotificationService`
doesn't change.

*Sender mode (opt-in).* `NotificationService:FcmTransport` is `Queue` (default; the
pipeline above) or `Sender`. In `Sender` mode the fan-out writes one
`notification_outbox` row per user device instead of an `fcm_queue` row, and
the host relays them over HTTP to the standalone sender app
(`server-side-senders/exs.sender-fcm`), then pulls delivery results back and
clears tokens FCM reports as dead from `user_session`. Required settings
(`NotificationService:FcmSender`): `BaseUrl`, `ApiKey`; optional `DefaultAccount`,
`AccountsByAppId` (client `AppUID` → sender account alias). Cutover per
environment: deploy the sender, set the three values, restart; switch back by
setting `FcmTransport` to `Queue`. The legacy `fcm-notification-sender` must keep
running until nothing is left in `fcm_queue`. The outbox tables belong to
`server-side-notifications` (`NotificationDatabaseContext` migrations); the
matching main-database migration is hand-emptied, as for the other notification tables.

*Email (opt-in, independent of the push transport).* `NotificationService:EmailTransport` is `Off` (default)
or `Sender`. In `Sender` mode email is relayed through `notification_outbox` rows (channel `email`) to the
standalone email sender (`server-side-senders/exs.sender-email`), which has its own address and API key under
`NotificationService:EmailSender` (same settings as `NotificationService:FcmSender`; `DefaultAccount` picks the
SMTP account, i.e. the From address). The channel is chosen per call on `INotificationSender`, not by notification
type (`Email:NotificationTypes`/`AllNotificationTypes` no longer exist). Every method writes into the caller's
repository, so it commits with the caller's own save:

- `CreateMobileNotification` / `CreateMobileDataNotification`: a push (title and body, or payload only) to the
  users' devices. Push only.
- `CreateEmailNotification`: same arguments, one email per user (title as subject, body as text) at the address
  `IEmailDestinationResolver` gives. Email only. With `EmailTransport` off the notification is marked `Expired`
  and a warning is logged.
- `CreateEmail(repository, EmailMessage)`: a free-form email to one address (password reset, invitation). It
  writes no `notification` row and names no user, only an outbox row whose envelope is cleared once the sender has
  accepted it. Throws `InvalidOperationException` unless `EmailTransport` is `Sender`, and `ArgumentException`
  for an invalid message.

With `EmailTransport=Sender` the host must register `services.AddEmailDestinationResolver<T>()` (here
`UserEmailDestinationResolver`, the address on the user record); the library's startup check demands one even
for a host that only sends free-form email. Optional: `AddEmailContentFactory<T>()` (default: title as subject,
body as text) and `AddEmailAddressInvalidator<T>()`. The tables belong to `server-side-notifications`, so the
matching main-database migration is hand-emptied: **before deploying this version, run**
`server-side-notifications/exs.notifications-database/Scripts/20261023133705_AddNotificationTransportType.sql`
(idempotent; adds `notification."TransportType"` and makes `notification_outbox."NotificationId"`/`"UserId"`
nullable).

**Health** — `/health/live` (process up) and `/health/ready` (DB reachable), both
anonymous, for orchestrator probes.

**Telemetry** — OpenTelemetry traces + metrics (`Program.cs`), custom instruments
in `core/Telemetry.cs` (`ActivitySource` for spans, `Meter` for metrics — new
sources must be registered in Program.cs via `AddSource`/`AddMeter`). Logs go
through Serilog: local rolling files (with `{TraceId}`) plus an OTLP sink. The
exporter activates only when `OpenTelemetry:OtlpEndpoint` (or the standard
`OTEL_EXPORTER_OTLP_ENDPOINT` env var) is set. The SPA sends W3C `traceparent`,
so traces begin in the browser.

Local observability backend (Grafana + Tempo + Loki + Mimir in one container):

```bash
docker compose -f telemetry/docker-compose.lgtm.yml up -d
```

UI at `http://localhost:3000` (see compose file for credentials).

**API versioning** — unversioned routes are v1 (`AssumeDefaultVersionWhenUnspecified`);
introduce breaking changes with `[ApiVersion("2.0")]` on the controller instead of
new route trees.

**GPS devices** — one `gps_device` table for every tracked thing: phones running our app (provider
`Mobile`, `SerialNumber` = the app's `DeviceUID`, no company) and Geotab trackers (provider `Geotab`,
`CompanyId` set, written by `server-side-feeders`). `gps_device.TypeId` points into the `gps_device_type`
tree (Mobile → Android / iOS; Geotab models such as GO9 are added by the feeders' asset sync); a device's
`ProviderId` must equal its type's. `(ProviderId, SerialNumber)` is unique, so a second login of the same
phone finds its device. The old `mobile_gps_device` / `mobile_gps_device_provider` tables are no longer
mapped: migration `20261028133018_20261028153014` copied their rows into `gps_device` **keeping the ids**
(session tokens and emergency alerts still point at the same device) and left the tables in place for one
release; drop them by hand afterwards. A phone's current state is written to the same
`gps_device_map_state` / `gps_device_state_data` rows as a tracker's (`PhoneMapState` in
`Controllers/MapData/Services`, used by the location-report worker): speed is stored in km/h (the wire
format keeps m/s), status is Moving above 3 km/h, Idle while the phone reports `in_vehicle`, else Parked,
an older fix never overwrites a newer one (a phone clock more than 5 minutes ahead is ignored), and the
battery level, charging flag and motion activity go into the state data JSON. `CompanyId` is null for
phones. A fix also sets `LocationAccuracyId` from the phone's accuracy and ends StalePosition (the status is
recomputed). Not done: `LowBattery` (the battery scale is not pinned down). The old `mobile_gps_device_map_state` rows were not copied (phones refill the new rows
as they report) and nothing reads that table any more; the track points still go to
`mobile_gps_device_track_point`.

Map state columns (migration `20261029102453_20261029122448`): `StatusId` is visibility (0 Invisible, 1 Visible,
default 1, more values may follow); `ActivityStatusId` is what `StatusId` used to hold (Unknown, Moving, Idle,
Parked, StalePosition; table `gps_device_activity_status`, renamed from `gps_device_map_status`, row 4 was
"Offline": `UPDATE gps_device_activity_status SET "Name" = 'StalePosition' WHERE "Id" = 4;`); `WarningFlags` is a
`GpsDeviceWarning` bit mask (LowBattery 1, EngineFault 2; bit 4 is no longer used: StalePosition is an activity
status now, set by the feeders' sweep when a device sends no position for the configured time); `LocationAccuracyId` is
`GPSLocationAccuracy` (not null, default Fine). `StatusChangedAt`, `LastCommunicationAt`, `LowBattery` and
`EngineFault` are no longer mapped; the columns stay in the table for one release (made nullable / defaulted)
and are dropped by hand. `gps_device.AssetTypeId` points at `gps_asset_type` (what the device is on: phone, car,
truck ...), separate from `TypeId` (the hardware). The feeders and `PhoneMapState` write the new columns; the
dates that `StatusChangedAt` / `LastCommunicationAt` held are the `activityStatus` / `communication` parameters of
`gps_device_state_data` (`StateValue<T>`, `DeviceStateData`).

**Feeder heartbeat** — `IFeederHeartbeat.CompanyWatched(companyId)` (in `Services/Feeders`) tells
the feeders (`server-side-feeders`) that somebody is looking at a company's map, so they poll
its accounts every ~2 s instead of every 30 s. Call it wherever a company's live map data is
served; it is cheap, never blocks, never throws, sends at most one `POST /v1/companies/{id}/live`
per company per 10 s and pauses for 30 s after a failure. It is **off** until
`Feeders:Heartbeat:BaseUrl` (and `ApiKey`, a secret: keep it in `local_configs`) is set; see
`server-side-feeders/README.md`. Counters: `app.feeder_heartbeat.sent|failed|dropped`.

## Trying the real email sender

`POST /api/test/email` sends one mail through the real email sender, `GET /api/test/email/{reference}` shows its
outcome. They are off (404) unless `TestEmail:Enabled` is `true`, because any logged-in user could otherwise send
mail from the company's SMTP account: set it only in your machine's own, uncommitted `api-server/local_configs/local_server.json`:

```json
"NotificationService": { "EmailTransport": "Sender", "EmailSender": {
  "BaseUrl": "http://127.0.0.1:5101/", "ApiKey": "<key made for this host>", "DefaultAccount": "<alias from the sender's Email:Accounts>" } },
"TestEmail": { "Enabled": true }
```

`BaseUrl` goes through an SSH tunnel (the deployed sender listens on loopback only). The sender needs a `Sender:Clients:<id>`
entry holding the SHA-256 of that key and the allowed account: `dotnet run --project ../server-side-senders/tools/exs.sender-email.TestClient -- keygen`
prints both the key and the entry. The database must have `server-side-notifications/exs.notifications-database/Scripts/20261017104840_AddNotificationOutbox.sql`
and then `20261023133705_AddNotificationTransportType.sql` applied first.

```bash
TOKEN=$(curl -s -X POST http://localhost:5199/api/auth/login -H 'Content-Type: application/json' \
  -d '{"UserName":"<user>","Password":"<password>"}' | jq -r .AccessToken)
curl -s -X POST http://localhost:5199/api/test/email -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"to":"<your address>","subject":"Test","text":"Hello"}'        # 202 {"reference":"<ref>"}
curl -s http://localhost:5199/api/test/email/<ref> -H "Authorization: Bearer $TOKEN"   # poll until "state":"completed"
```

The status never contains the address or the text; `resultStatus` is `sent` when the sender's SMTP server took the mail.

## Adding a feature (the golden path)

1. Entity in `db-model`, mapping in `main-database/DatabaseContext` partial.
2. Service (+ interface if it does I/O or has rules worth testing) next to its
   controller; register in `core/DiHelper.cs`.
3. Controller inheriting `AuthAPIController`, explicit route (`/api/...`), DTOs
   in a `Dto/` subfolder.
4. Integration tests: a new test class in the `"api"` collection; call
   `_fixture.ResetDatabaseAsync()` in `InitializeAsync`, use
   `CreateAuthenticatedClientAsync()` and assert through the DB via `QueryDbAsync`.
5. Custom telemetry where real work happens: a span for the operation, a metric
   if you would alert on it.

## Known limitations / TODO

- **Passwords are stored in plain text** (`AuthService.VerifyPassword` TODO) —
  must move to PBKDF2/Argon2 before any real deployment.
- Secrets (DB password, JWT key) live in `local_configs` in the repo — move to
  environment variables / user-secrets and rotate.
- Idempotency store is in-memory (single instance).
- No CI pipeline yet (`dotnet test` is all a runner needs, plus Docker).
- Schema is not in the repo (no EF migrations); integration tests create it via
  `EnsureCreated` and add `LatestUpdate` defaults manually.
