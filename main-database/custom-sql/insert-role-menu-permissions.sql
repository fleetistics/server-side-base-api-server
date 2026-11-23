-- The first default permissions on menu 1 ("Main"): Administrator may use every item, the Portal Admin branch included (temporary:
-- the Portal Admin pages are for the Administrator role for now). Every other role gets nothing from this script; their default
-- rights are edited in the portal on Portal Admin > Roles (a company can override them on Admin > User Management > Roles).
--
-- These are DEFAULT rows (CompanyId NULL) of a system role. A company's own rows (CompanyId = the company) replace them for that role.
-- All four flags (CanView, CanEdit, CanDelete, CanCreate) are true. There is no "Portal Admin" role any more.
-- Run insert-menu-items.sql first; this script stops with an error when the menu or the role is missing.
-- The partial unique index UX_role2menu_item_default (migration 20261123140437) makes a re-run safe: an existing default row of
-- (role, item) is left as it is, so a flag changed in the portal stays. A re-run does not remove rows.

DO $$
BEGIN
	IF NOT EXISTS (SELECT 1 FROM application_menu_item WHERE "MenuId" = 1) THEN
		RAISE EXCEPTION 'menu 1 has no items: run insert-menu-items.sql first';
	END IF;
	IF NOT EXISTS (SELECT 1 FROM permission_role WHERE "Name" = 'Administrator' AND "CompanyId" IS NULL) THEN
		RAISE EXCEPTION 'the role Administrator does not exist: run insert-permission-roles.sql first';
	END IF;
END $$;

INSERT INTO role2menu_item ("PermissionRoleId", "ApplicationMenuItemId", "CompanyId", "CanView", "CanEdit", "CanDelete", "CanCreate")
SELECT r."Id", i."Id", NULL, true, true, true, true
FROM application_menu_item i
JOIN permission_role r ON r."CompanyId" IS NULL AND r."Name" = 'Administrator'
WHERE i."MenuId" = 1
ON CONFLICT ("PermissionRoleId", "ApplicationMenuItemId") WHERE "CompanyId" IS NULL DO NOTHING;

-- Check: default items per role.
-- SELECT r."Name", count(*) FROM role2menu_item x JOIN permission_role r ON r."Id" = x."PermissionRoleId" WHERE x."CompanyId" IS NULL GROUP BY r."Name" ORDER BY 1;
