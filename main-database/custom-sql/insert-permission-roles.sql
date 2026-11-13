-- System-wide permission roles (CompanyId = NULL, IsDefault = NULL).
-- Id is generated, LatestUpdate defaults to now(). Safe to re-run: a role that already exists is skipped.
INSERT INTO permission_role ("Name", "CompanyId", "IsDefault", "Description")
SELECT v."Name", NULL, NULL, NULL
FROM (VALUES
    ('Administrator'),
    ('Fleet Manager'),
    ('Dispatcher'),
    ('Billing'),
    ('Technician'),
    ('Read-Only')
) AS v("Name")
WHERE NOT EXISTS (
    SELECT 1 FROM permission_role r
    WHERE r."Name" = v."Name" AND r."CompanyId" IS NULL
);
