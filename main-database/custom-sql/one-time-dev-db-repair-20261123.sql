-- One-time repair of the shared dev database (base-dev-db). Run ONCE, by hand. Not for any other database.
--
-- Why: migration 20261118150355 (kav branch) was applied there. It renamed SessionKey / ClientInfo_PlatformDetails /
-- ClientInfo_AppHash to ClientInfo_DeviceUID / ClientInfo_CodeVersion / ClientInfo_AppUID, dropped
-- IX_user_session_SessionKey and widened client_application.Id to integer; the current columns were re-created
-- afterwards. Its counterpart 20261118150918 then failed at startup ("column SessionKey already exists").
-- Both migrations are now empty in code; this brings the dev database to the same state.
--
-- It also replaces the history of the location-token columns: they were added there by two migrations that were
-- since deleted (20261122194729, 20261122232007) and are now added by 20261123162746, which must not run again.
--
-- Checked read-only before writing (2026-11-23): the three leftover columns hold no data (13 rows), there are no
-- duplicate SessionKey values, client_application is empty and has no foreign keys.
-- After it runs, application startup applies only the empty 20261118150918.

BEGIN;

-- Leftovers of the rename (no code maps them any more).
ALTER TABLE user_session
    DROP COLUMN "ClientInfo_AppUID",
    DROP COLUMN "ClientInfo_CodeVersion",
    DROP COLUMN "ClientInfo_DeviceUID";

-- Back to the model's type (smallint identity).
ALTER TABLE client_application ALTER COLUMN "Id" TYPE smallint;

-- The model's unique index; refresh keys are looked up by SessionKey.
CREATE UNIQUE INDEX "IX_user_session_SessionKey" ON user_session ("SessionKey");

-- Location-token columns: same columns, now owned by 20261123162746.
DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" IN ('20261122194729_20261122204726', '20261122232007_20261123002004');
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261123162746_20261123172743', '10.0.13');

COMMIT;
