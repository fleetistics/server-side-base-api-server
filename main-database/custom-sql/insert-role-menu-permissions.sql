-- Which roles may see (and use) which items of menu 1 ("Main"). A role without a row for an item does not see it.
--
--   Portal Admin   every item of menu 1.
--   Administrator  every item of menu 1 except the Portal Admin branch (the item with Path '/portal-admin' and everything below it).
--   Fleet Manager, Dispatcher, Billing, Technician, Read-Only: nothing for now (no rows).
--
-- Both roles get all four flags (CanView, CanEdit, CanDelete, CanCreate) true.
-- Run insert-menu-items.sql and insert-portal-admin-role.sql first; this script stops with an error when either is missing.
-- LatestUpdate defaults to now(). Safe to re-run: an existing (role, item) row is left as it is, so a flag changed by hand stays.
-- Note: a re-run does not remove rows; to take an item away from a role, delete its row.

DO $$
BEGIN
	IF NOT EXISTS (SELECT 1 FROM application_menu_item WHERE "MenuId" = 1 AND "Path" = '/portal-admin') THEN
		RAISE EXCEPTION 'menu 1 has no item with Path /portal-admin: run insert-menu-items.sql first';
	END IF;
	IF NOT EXISTS (SELECT 1 FROM permission_role WHERE "Name" = 'Portal Admin' AND "CompanyId" IS NULL) THEN
		RAISE EXCEPTION 'the role Portal Admin does not exist: run insert-portal-admin-role.sql first';
	END IF;
	IF NOT EXISTS (SELECT 1 FROM permission_role WHERE "Name" = 'Administrator' AND "CompanyId" IS NULL) THEN
		RAISE EXCEPTION 'the role Administrator does not exist: run insert-permission-roles.sql first';
	END IF;
END $$;

WITH RECURSIVE portal_admin_branch AS (
	SELECT "Id" FROM application_menu_item WHERE "MenuId" = 1 AND "Path" = '/portal-admin'
	UNION ALL
	SELECT c."Id" FROM application_menu_item c JOIN portal_admin_branch p ON c."ParentId" = p."Id" WHERE c."MenuId" = 1
)
INSERT INTO role2menu_item ("PermissionRoleId", "ApplicationMenuItemId", "CanView", "CanEdit", "CanDelete", "CanCreate")
SELECT r."Id", i."Id", true, true, true, true
FROM application_menu_item i
JOIN permission_role r ON r."CompanyId" IS NULL AND (
	r."Name" = 'Portal Admin'
	OR (r."Name" = 'Administrator' AND i."Id" NOT IN (SELECT "Id" FROM portal_admin_branch)))
WHERE i."MenuId" = 1
ON CONFLICT ("PermissionRoleId", "ApplicationMenuItemId") DO NOTHING;

-- Check: items per role (expect Portal Admin 110, Administrator 95: the Portal Admin branch is 15 items).
-- SELECT r."Name", count(*) FROM role2menu_item x JOIN permission_role r ON r."Id" = x."PermissionRoleId" GROUP BY r."Name" ORDER BY 1;
