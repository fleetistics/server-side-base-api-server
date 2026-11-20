-- Loads the portal's navigation tree into application_menu_item for menu 1 ("Main").
-- Source: client-side-base-portal/src/data/nav-data.ts (NAV). Generated from that file: 110 items, depth-first.
--
--   Title     NavItem.name
--   Icon      NavItem.icon (the portal's icon name, e.g. 'Clipboard')
--   Path      the portal route, '/' + NavItem.id (the sidebar navigates to "/" + id; src/pages/<id>.tsx or <id>/index.tsx).
--             NULL for the 6 groups that are not pages of their own (not in PARENT_PAGE_IDS; clicking them only expands):
--             contacts/sales, reports, add-ons, add-ons/citizen-manager, add-ons/roadside, admin/security.
--   ParentId  the Id of the parent item (children are inserted after their parent, so it is known by then).
--   OrderBy   1-based position among its siblings, as in nav-data.ts.
--   Left NULL: Description, DividerBefore, DividerAfter, StatusId (its meaning is not defined anywhere I could find),
--              ApplicationPageId (application_page is empty and not linked here).
--
-- Safe to run twice: it does nothing when menu 1 already has items, and stops when menu 1 does not exist.
-- Runs in one transaction (a single DO block). Check afterwards with the query at the end of the file.

DO $$
DECLARE
	v_menu_id constant integer := 1;
	r record;
	v_id integer;
BEGIN
	IF NOT EXISTS (SELECT 1 FROM application_menu WHERE "Id" = v_menu_id) THEN
		RAISE EXCEPTION 'application_menu % does not exist', v_menu_id;
	END IF;

	IF EXISTS (SELECT 1 FROM application_menu_item WHERE "MenuId" = v_menu_id) THEN
		RAISE NOTICE 'application_menu_item already has items for menu %, nothing inserted', v_menu_id;
		RETURN;
	END IF;

	CREATE TEMP TABLE nav_stage (
		seq serial,
		nav_key text PRIMARY KEY,   -- NavItem.id
		parent_key text,            -- NavItem.id of the parent, NULL at the top level
		title text NOT NULL,
		icon text,
		ord smallint NOT NULL,
		path text,
		new_id integer
	) ON COMMIT DROP;

	INSERT INTO nav_stage (nav_key, parent_key, title, icon, ord, path) VALUES
		('home', NULL, 'Home', 'Home', 1, '/home'),
		('dashboard', NULL, 'Dashboard', 'Dashboard', 2, '/dashboard'),
		('map', NULL, 'Map', 'Map', 3, '/map'),
		('jobs', NULL, 'Jobs', 'Clipboard', 4, '/jobs'),
		('jobs/schedule', 'jobs', 'Calendar', 'Calendar', 1, '/jobs/schedule'),
		('jobs/progress', 'jobs', 'Progress', 'Activity', 2, '/jobs/progress'),
		('jobs/tasks', 'jobs', 'Tasks', 'CheckSquare', 3, '/jobs/tasks'),
		('jobs/quotes', 'jobs', 'Quote', 'FileText', 4, '/jobs/quotes'),
		('jobs/field-reminders', 'jobs', 'Jobs', 'Bell', 5, '/jobs/field-reminders'),
		('jobs/invoices', 'jobs', 'Invoice', 'Receipt', 6, '/jobs/invoices'),
		('jobs/payments', 'jobs', 'Payment', 'CreditCard', 7, '/jobs/payments'),
		('contacts', NULL, 'Contacts', 'Users', 5, '/contacts'),
		('contacts/dashboard', 'contacts', 'Dashboard', 'Dashboard', 1, '/contacts/dashboard'),
		('contacts/companies', 'contacts', 'Companies', 'Building', 2, '/contacts/companies'),
		('contacts/list', 'contacts', 'Contacts', 'User', 3, '/contacts/list'),
		('contacts/activities', 'contacts', 'Activities', 'Activity', 4, '/contacts/activities'),
		('contacts/sales', 'contacts', 'Sales', 'Target', 5, NULL),
		('contacts/sales/dashboard', 'contacts/sales', 'Dashboard', 'Dashboard', 1, '/contacts/sales/dashboard'),
		('contacts/sales/pipeline', 'contacts/sales', 'Pipeline', 'GitBranch', 2, '/contacts/sales/pipeline'),
		('contacts/sales/opportunities', 'contacts/sales', 'Opportunities', 'Target', 3, '/contacts/sales/opportunities'),
		('contacts/sales/quotes', 'contacts/sales', 'Quotes', 'FileText', 4, '/contacts/sales/quotes'),
		('contacts/sales/invoices', 'contacts/sales', 'Invoices', 'Receipt', 5, '/contacts/sales/invoices'),
		('contacts/sales/reports', 'contacts/sales', 'Reports', 'Trophy', 6, '/contacts/sales/reports'),
		('contacts/messaging', 'contacts', 'Messaging', 'Message', 6, '/contacts/messaging'),
		('support-training', NULL, 'Support & Training', 'GraduationCap', 6, '/support-training'),
		('support-training/cases', 'support-training', 'Support Cases', 'Headset', 1, '/support-training/cases'),
		('support-training/training', 'support-training', 'Training', 'GraduationCap', 2, '/support-training/training'),
		('support-training/documentation', 'support-training', 'Documentation', 'FileText', 3, '/support-training/documentation'),
		('reports', NULL, 'Reports', 'FileText', 7, NULL),
		('reports/all', 'reports', 'All Reports', 'FileText', 1, '/reports/all'),
		('reports/scheduled', 'reports', 'Scheduled Reports', 'Calendar', 2, '/reports/scheduled'),
		('media-viewer', NULL, 'Media Viewer', 'Film', 8, '/media-viewer'),
		('quick-order', NULL, 'Quick Order', 'Cart', 9, '/quick-order'),
		('add-ons', NULL, 'Add-ons', 'Package', 10, NULL),
		('add-ons/citizen-manager', 'add-ons', 'Citizen Manager', 'Headset', 1, NULL),
		('add-ons/citizen-manager/tickets', 'add-ons/citizen-manager', 'Reported Issues', 'Clipboard', 1, '/add-ons/citizen-manager/tickets'),
		('add-ons/citizen-manager/report-form', 'add-ons/citizen-manager', 'Report Form', 'ExternalLink', 2, '/add-ons/citizen-manager/report-form'),
		('add-ons/citizen-manager/reports', 'add-ons/citizen-manager', 'Reports', 'FileText', 3, '/add-ons/citizen-manager/reports'),
		('add-ons/driver-manager', 'add-ons', 'Driver Manager', 'UserCog', 2, '/add-ons/driver-manager'),
		('add-ons/driver-manager/dashboard', 'add-ons/driver-manager', 'Dashboard', 'Dashboard', 1, '/add-ons/driver-manager/dashboard'),
		('add-ons/driver-manager/drivers', 'add-ons/driver-manager', 'Drivers', 'Users', 2, '/add-ons/driver-manager/drivers'),
		('add-ons/driver-manager/groups', 'add-ons/driver-manager', 'Groups', 'UserCog', 3, '/add-ons/driver-manager/groups'),
		('add-ons/driver-manager/hiring', 'add-ons/driver-manager', 'Hiring Pipeline', 'Briefcase', 4, '/add-ons/driver-manager/hiring'),
		('add-ons/driver-manager/background', 'add-ons/driver-manager', 'Checks', 'Shield', 5, '/add-ons/driver-manager/background'),
		('add-ons/driver-manager/training', 'add-ons/driver-manager', 'Training', 'GraduationCap', 6, '/add-ons/driver-manager/training'),
		('add-ons/fleet', 'add-ons', 'Fleet', 'Truck', 3, '/add-ons/fleet'),
		('add-ons/fleet/work-orders', 'add-ons/fleet', 'Maintenance Order', 'FileText', 1, '/add-ons/fleet/work-orders'),
		('add-ons/fleet/reports', 'add-ons/fleet', 'Reports', 'FileText', 2, '/add-ons/fleet/reports'),
		('add-ons/fleet/incidents', 'add-ons/fleet', 'Incident Management', 'AlertTriangle', 3, '/add-ons/fleet/incidents'),
		('add-ons/fleet/maintenance', 'add-ons/fleet', 'Maintenance', 'Wrench', 4, '/add-ons/fleet/maintenance'),
		('add-ons/fleet/technicians', 'add-ons/fleet', 'Technicians', 'Users', 5, '/add-ons/fleet/technicians'),
		('add-ons/fleet/administration', 'add-ons/fleet', 'Administration', 'Settings', 6, '/add-ons/fleet/administration'),
		('add-ons/fleet/groups', 'add-ons/fleet', 'Groups', 'Folder', 7, '/add-ons/fleet/groups'),
		('add-ons/fleet/geofences', 'add-ons/fleet', 'Geofences', 'MapPin', 8, '/add-ons/fleet/geofences'),
		('add-ons/form-builder', 'add-ons', 'Form Builder', 'Clipboard', 4, '/add-ons/form-builder'),
		('add-ons/fuel', 'add-ons', 'Fuel', 'Fuel', 5, '/add-ons/fuel'),
		('add-ons/fuel/dashboard', 'add-ons/fuel', 'Dashboard', 'Dashboard', 1, '/add-ons/fuel/dashboard'),
		('add-ons/fuel/transactions', 'add-ons/fuel', 'Fuel Cards', 'CreditCard', 2, '/add-ons/fuel/transactions'),
		('add-ons/fuel/reports', 'add-ons/fuel', 'Reports', 'FileText', 3, '/add-ons/fuel/reports'),
		('add-ons/inventory', 'add-ons', 'Inventory', 'Package', 6, '/add-ons/inventory'),
		('add-ons/inventory/stock', 'add-ons/inventory', 'Stock', 'Package', 1, '/add-ons/inventory/stock'),
		('add-ons/inventory/orders', 'add-ons/inventory', 'Orders', 'Cart', 2, '/add-ons/inventory/orders'),
		('add-ons/inventory/checkouts', 'add-ons/inventory', 'Check-Outs', 'Clipboard', 3, '/add-ons/inventory/checkouts'),
		('add-ons/kiosk', 'add-ons', 'Kiosk', 'Clock', 7, '/add-ons/kiosk'),
		('add-ons/roadside', 'add-ons', 'Roadside', 'AlertTriangle', 8, NULL),
		('add-ons/roadside/events', 'add-ons/roadside', 'Roadside Events', 'AlertTriangle', 1, '/add-ons/roadside/events'),
		('add-ons/roadside/log', 'add-ons/roadside', 'Event Log', 'Clipboard', 2, '/add-ons/roadside/log'),
		('add-ons/roadside/settings', 'add-ons/roadside', 'Settings', 'Settings', 3, '/add-ons/roadside/settings'),
		('add-ons/rentals', 'add-ons', 'Rentals', 'Briefcase', 9, '/add-ons/rentals'),
		('add-ons/rentals/calendar', 'add-ons/rentals', 'Calendar', 'Calendar', 1, '/add-ons/rentals/calendar'),
		('add-ons/rentals/active', 'add-ons/rentals', 'Active Rentals', 'Clipboard', 2, '/add-ons/rentals/active'),
		('add-ons/rentals/equipment', 'add-ons/rentals', 'Equipment', 'Truck', 3, '/add-ons/rentals/equipment'),
		('admin', NULL, 'Admin', 'Settings', 11, '/admin'),
		('admin/billing', 'admin', 'Billing', 'Receipt', 1, '/admin/billing'),
		('admin/billing/subscription', 'admin/billing', 'Subscription', 'CreditCard', 1, '/admin/billing/subscription'),
		('admin/billing/addons', 'admin/billing', 'Modules & Connections', 'Package', 2, '/admin/billing/addons'),
		('admin/billing/quotes', 'admin/billing', 'Quotes', 'FileText', 3, '/admin/billing/quotes'),
		('admin/billing/orders', 'admin/billing', 'Orders', 'Cart', 4, '/admin/billing/orders'),
		('admin/billing/invoices', 'admin/billing', 'Invoices', 'Receipt', 5, '/admin/billing/invoices'),
		('admin/billing/products-services-pricing', 'admin/billing', 'Products & Services', 'Package', 6, '/admin/billing/products-services-pricing'),
		('admin/billing/statement', 'admin/billing', 'Statement', 'FileText', 7, '/admin/billing/statement'),
		('admin/billing/payment-details', 'admin/billing', 'Payment Details', 'CreditCard', 8, '/admin/billing/payment-details'),
		('admin/billing/e-signature', 'admin/billing', 'e-Signature', 'Edit', 9, '/admin/billing/e-signature'),
		('admin/configuration', 'admin', 'Configuration', 'Settings', 2, '/admin/configuration'),
		('admin/communication', 'admin', 'Communication', 'Message', 3, '/admin/communication'),
		('admin/canned-messages', 'admin', 'Messages', 'Message', 4, '/admin/canned-messages'),
		('admin/links', 'admin', 'Links', 'ExternalLink', 5, '/admin/links'),
		('admin/integrations', 'admin', 'Integrations', 'Plug', 6, '/admin/integrations'),
		('admin/kpi-standards', 'admin', 'KPIs', 'Gauge', 7, '/admin/kpi-standards'),
		('admin/notifications', 'admin', 'Notifications', 'Bell', 8, '/admin/notifications'),
		('admin/security', 'admin', 'Security', 'Lock', 9, NULL),
		('admin/security/settings', 'admin/security', 'Settings', 'Settings', 1, '/admin/security/settings'),
		('admin/security/role-manager', 'admin/security', 'Role Manager', 'UserCog', 2, '/admin/security/role-manager'),
		('admin/security/audit-log', 'admin/security', 'Audit Log', 'Activity', 3, '/admin/security/audit-log'),
		('admin/user-management', 'admin', 'User Management', 'Users', 10, '/admin/user-management'),
		('portal-admin', NULL, 'Portal Admin', 'Shield', 12, '/portal-admin'),
		('portal-admin/account-access', 'portal-admin', 'Account Access', 'Shield', 1, '/portal-admin/account-access'),
		('portal-admin/roles', 'portal-admin', 'Roles', 'UserCog', 2, '/portal-admin/roles'),
		('portal-admin/cases', 'portal-admin', 'Cases', 'Headset', 3, '/portal-admin/cases'),
		('portal-admin/accounting', 'portal-admin', 'Accounting', 'Receipt', 4, '/portal-admin/accounting'),
		('portal-admin/accounting/summary', 'portal-admin/accounting', 'Billing Dashboard', 'FileText', 1, '/portal-admin/accounting/summary'),
		('portal-admin/accounting/detail', 'portal-admin/accounting', 'Billing Detail', 'FileText', 2, '/portal-admin/accounting/detail'),
		('portal-admin/accounting/payment-issues', 'portal-admin/accounting', 'Payment Issues', 'Alert', 3, '/portal-admin/accounting/payment-issues'),
		('portal-admin/accounting/account-status', 'portal-admin/accounting', 'Account Status', 'CheckCircle', 4, '/portal-admin/accounting/account-status'),
		('portal-admin/developer', 'portal-admin', 'Developer', 'Code', 5, '/portal-admin/developer'),
		('portal-admin/developer/menu-admin', 'portal-admin/developer', 'Menu Admin', 'Menu', 1, '/portal-admin/developer/menu-admin'),
		('portal-admin/developer/connections', 'portal-admin/developer', 'Connections', 'Plug', 2, '/portal-admin/developer/connections'),
		('portal-admin/developer/data-management', 'portal-admin/developer', 'Data Management', 'Database', 3, '/portal-admin/developer/data-management'),
		('portal-admin/developer/change-log', 'portal-admin/developer', 'Change Log', 'Clock', 4, '/portal-admin/developer/change-log'),
		('portal-admin/developer/message-templates', 'portal-admin/developer', 'Message Templates', 'Message', 5, '/portal-admin/developer/message-templates');

	-- seq follows the order above (depth-first), so a parent always has its Id before its children are inserted.
	FOR r IN SELECT * FROM nav_stage ORDER BY seq LOOP
		INSERT INTO application_menu_item ("MenuId", "Title", "Icon", "ParentId", "OrderBy", "Path")
		VALUES (
			v_menu_id, r.title, r.icon,
			(SELECT p.new_id FROM nav_stage p WHERE p.nav_key = r.parent_key),
			r.ord, r.path)
		RETURNING "Id" INTO v_id;

		UPDATE nav_stage SET new_id = v_id WHERE nav_key = r.nav_key;
	END LOOP;

	RAISE NOTICE 'inserted % items into application_menu_item for menu %', (SELECT count(*) FROM nav_stage), v_menu_id;
END $$;

-- Check: the tree, indented by depth (expect 110 rows, 104 with a Path).
-- WITH RECURSIVE t AS (
--     SELECT "Id", "ParentId", "Title", "Path", "OrderBy", 0 AS depth, lpad("OrderBy"::text, 3, '0') AS sort
--     FROM application_menu_item WHERE "MenuId" = 1 AND "ParentId" IS NULL
--   UNION ALL
--     SELECT c."Id", c."ParentId", c."Title", c."Path", c."OrderBy", t.depth + 1, t.sort || '.' || lpad(c."OrderBy"::text, 3, '0')
--     FROM application_menu_item c JOIN t ON c."ParentId" = t."Id" WHERE c."MenuId" = 1)
-- SELECT repeat('  ', depth) || "Title" AS item, "Path" FROM t ORDER BY sort;
