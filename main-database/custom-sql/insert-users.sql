-- Demo users. Everything not in the screenshot is a default:
--   UserName = lower-case e-mail (Auth:EmailMode UniqueEmail makes the e-mail the user name), DisplayName = FullName,
--   StatusId = 1 (UserStatus.Active), UserSourceId = 5 (UserSource.Manual),
--   IsMfaEnabled / IsEmailVerified / IsPhoneVerified = false, Phone and the avatar / gov id images = NULL.
--   Password = NULL: these users cannot sign in until a password is set (password reset or the register flow);
--   a hash can only be made by the app's PasswordHasher, not in SQL.
-- "CompanyId" is the company the users belong to (the same company id as insert-user-groups.sql).
-- >>> Set the company id in the first line of the CTE below before running. <<<
-- Id is generated, LatestUpdate defaults to now(). Safe to re-run: a user whose UserName already exists is skipped.
WITH target AS (SELECT 1 AS company_id)  -- <<< company id
INSERT INTO "user" ("CompanyId", "UserName", "DisplayName", "FullName", "Email",
                    "StatusId", "UserSourceId", "Password", "IsMfaEnabled", "IsEmailVerified", "IsPhoneVerified")
SELECT t.company_id, lower(v."Email"), v."FullName", v."FullName", v."Email",
       1::smallint, 5::smallint, NULL, false, false, false
FROM target t
CROSS JOIN (VALUES
    ('John Smith',      'john.smith@fleetistics.com'),
    ('Sarah Williams',  'sarah.w@andersonlog.com'),
    ('Mike Johnson',    'mike.j@andersonlog.com'),
    ('Lisa Anderson',   'lisa.a@andersonlog.com'),
    ('Robert Diamond',  'rob@andersonlog.com'),
    ('David Kim',       'david.k@andersonlog.com'),
    ('Patricia Lee',    'patricia@andersonlog.com'),
    ('James Wilson',    'james.w@andersonlog.com'),
    ('Maria Rodriguez', 'maria.r@andersonlog.com'),
    ('Kevin Brooks',    'k.brooks@andersonlog.com')
) AS v("FullName", "Email")
WHERE NOT EXISTS (
    SELECT 1 FROM "user" u
    WHERE lower(u."UserName") = lower(v."Email")
);
