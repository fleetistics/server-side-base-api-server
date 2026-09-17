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
