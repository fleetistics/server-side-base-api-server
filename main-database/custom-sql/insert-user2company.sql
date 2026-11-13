-- Links every user of company 1 ("user"."CompanyId" = 1) to that company.
-- PermissionRoleId = 1, StatusId = 1, JoinedDate = now(), as requested.
-- JoinTypeId was not specified: 1 (User2CompanyJoinType.Manual).
-- Note: user2company_status 1 is "Creating"; "Active" is 2 (see insert-ref-tables.sql).
-- LatestUpdate defaults to now(). Safe to re-run: a user already linked to the company is skipped.
INSERT INTO user2company ("CompanyId", "UserId", "PermissionRoleId", "StatusId", "JoinTypeId", "JoinedDate")
SELECT u."CompanyId", u."Id", 1, 1, 1, now()
FROM "user" u
WHERE u."CompanyId" = 1
ON CONFLICT ("CompanyId", "UserId") DO NOTHING;
