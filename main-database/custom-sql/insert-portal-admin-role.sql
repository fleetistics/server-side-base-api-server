-- The system-wide role 'Portal Admin' (CompanyId = NULL, like the roles in insert-permission-roles.sql), and user 28 as its member.
-- The menu permissions of the role are in insert-role-menu-permissions.sql (run this script first).
--
-- User 28 ("kav") has "user"."CompanyId" = 1 but no user2company row: insert-user2company.sql ran before the account existed.
-- The row is made like that script's rows (JoinTypeId 1 = Manual, JoinedDate = now()), with two differences:
--   PermissionRoleId = Portal Admin, and StatusId = 2 ("Active"; the older rows say 1, "Creating", as that script was asked to).
-- If user 28 already has a membership in company 1, only its role is changed to Portal Admin.
-- A user has one role per company, so this replaces any other role (such as Administrator): Portal Admin gets every permission.
--
-- Id and LatestUpdate are generated. Safe to re-run.

INSERT INTO permission_role ("Name", "CompanyId", "IsDefault", "Description")
SELECT 'Portal Admin', NULL, NULL, 'Everything, including the Portal Admin menu.'
WHERE NOT EXISTS (
    SELECT 1 FROM permission_role r
    WHERE r."Name" = 'Portal Admin' AND r."CompanyId" IS NULL
);

INSERT INTO user2company ("CompanyId", "UserId", "PermissionRoleId", "StatusId", "JoinTypeId", "JoinedDate")
SELECT 1, 28, r."Id", 2, 1, now()
FROM permission_role r
WHERE r."Name" = 'Portal Admin' AND r."CompanyId" IS NULL
ON CONFLICT ("CompanyId", "UserId") DO UPDATE SET "PermissionRoleId" = EXCLUDED."PermissionRoleId";

-- Check:
-- SELECT u."CompanyId", u."UserId", r."Name" AS role, u."StatusId" FROM user2company u JOIN permission_role r ON r."Id" = u."PermissionRoleId" WHERE u."UserId" = 28;
