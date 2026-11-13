-- User groups of ONE company (user_group.CompanyId is required and has a foreign key to company).
-- >>> Set the company id in the first line of the CTE below before running. <<<
-- GroupColor is db_model.Constants.UIElementColor: 1 Red, 2 Green, 3 Blue, 4 Amber, 5 Indigo, 6 Slate.
-- Id is generated, LatestUpdate defaults to now(). Safe to re-run: a group with the same name in the company is skipped.
--
-- Descriptions are NULL: the screenshot cuts them off, so the full text is not known. What it shows:
--   Monday Report Group          "Receives the weekly fleet summary every Mo..."
--   Safety Exception Alert Group "Notified of speeding, harsh braking and other ..."
--   Management Group             "Leadership notices, cost and compliance sum..."
--   Driver Group                 "Driver bulletins, schedule changes and policy ..."
--   Ops Dept Group               "Day-to-day operations and dispatch announc..."
WITH target AS (SELECT 1 AS company_id)  -- <<< company id
INSERT INTO user_group ("Name", "CompanyId", "GroupColor", "Description")
SELECT v."Name", t.company_id, v."GroupColor", NULL
FROM target t
CROSS JOIN (VALUES
    ('Monday Report Group',          3::smallint),  -- blue
    ('Safety Exception Alert Group', 1::smallint),  -- red
    ('Management Group',             3::smallint),  -- blue
    ('Driver Group',                 2::smallint),  -- green
    ('Ops Dept Group',               4::smallint)   -- amber
) AS v("Name", "GroupColor")
WHERE NOT EXISTS (
    SELECT 1 FROM user_group g
    WHERE g."CompanyId" = t.company_id AND g."Name" = v."Name"
);
