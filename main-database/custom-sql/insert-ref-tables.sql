-- Rows of the ref (status / type / source) tables, one per constant of the matching db-model class.
-- The Id is the contract with the code (the constants); Name is only a readable label.
-- LatestUpdate defaults to now(). Safe to re-run: ON CONFLICT DO NOTHING keeps rows that already exist
-- (it does not rename them). Never reuse or renumber an Id; to retire a value add a new one.
--
-- Not here: gps_device_type and gps_asset_type (seeded by the model itself, HasData), settings_entry (same),
-- permission_role / user_group / user (see their own insert-*.sql files). TeamUserRole and MediaStatus have
-- constants but no table is mapped for them yet, so there is nothing to insert into.

-- ===== Users =====
INSERT INTO user_status ("Id", "Name") VALUES
    (0,  'Just Created'),
    (1,  'Active'),
    (13, 'Inactive')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO user_source ("Id", "Name") VALUES
    (1,  'Auto (Device UID)'),
    (2,  'Registration (Portal)'),
    (3,  'Registration (App)'),
    (4,  'MF Migrate'),
    (5,  'Manual'),
    (13, 'Test User')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO user_emergency_alert_status ("Id", "Name") VALUES
    (0,  'Just Created'),
    (1,  'Active'),
    (2,  'Closed By User'),
    (3,  'Expired'),
    (13, 'Cancelled')
ON CONFLICT ("Id") DO NOTHING;

-- ===== Companies =====
INSERT INTO company_status ("Id", "Name") VALUES
    (1,  'Active'),
    (13, 'Inactive')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO user2company_status ("Id", "Name") VALUES
    (1, 'Creating'),
    (2, 'Active'),
    (3, 'With Other Company'),
    (4, 'Left')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO user2company_join_type ("Id", "Name") VALUES
    (1, 'Manual'),
    (2, 'Invited'),
    (3, 'External Sync')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO user_join_invitation_status ("Id", "Name") VALUES
    (1, 'Active'),
    (2, 'Expired'),
    (3, 'Accepted'),
    (4, 'Cancelled')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO user_join_request_status ("Id", "Name") VALUES
    (1, 'Requested By User'),
    (2, 'Auto Accepted'),
    (3, 'Accepted'),
    (4, 'Rejected'),
    (5, 'Cancelled')
ON CONFLICT ("Id") DO NOTHING;

-- ===== Teams =====
INSERT INTO team_status ("Id", "Name") VALUES
    (0, 'Generate Key'),
    (1, 'Creating'),
    (2, 'Active'),
    (3, 'Completed'),
    (4, 'Cancelled'),
    (5, 'Expired')
ON CONFLICT ("Id") DO NOTHING;

-- TeamMemberUserStatus
INSERT INTO team_user_status ("Id", "Name") VALUES
    (1, 'Creating'),
    (2, 'Active'),
    (3, 'With Other Team'),
    (4, 'Left')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO team_primary_target_user_status ("Id", "Name") VALUES
    (1, 'Active'),
    (2, 'Cancelled')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO team_activity_type ("Id", "Name") VALUES
    (1, 'Member Joined'),
    (2, 'Member Left'),
    (3, 'Member Switched Out'),
    (4, 'Member Switched In'),
    (5, 'Member Report')
ON CONFLICT ("Id") DO NOTHING;

-- ===== Messages =====
INSERT INTO user_message_status ("Id", "Name") VALUES
    (1, 'Active')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO user_message2user_status ("Id", "Name") VALUES
    (1, 'Read')
ON CONFLICT ("Id") DO NOTHING;

-- ===== GPS devices and map =====
-- Ids 1 and 2 were the old iOS / Android providers: never reuse them.
INSERT INTO gps_device_provider ("Id", "Name") VALUES
    (0, 'Unknown'),
    (3, 'Geotab'),
    (4, 'Mobile')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO gps_device_status ("Id", "Name") VALUES
    (1,  'Active'),
    (12, 'Mobile Log Out Inactive'),
    (13, 'Inactive')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO gps_location_accuracy ("Id", "Name") VALUES
    (1, 'Fine'),
    (2, 'Fair'),
    (3, 'Poor'),
    (4, 'Bad')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO gps_device_map_status ("Id", "Name") VALUES
    (0, 'Invisible'),
    (1, 'Visible')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO gps_device_activity_status ("Id", "Name") VALUES
    (0, 'Unknown'),
    (1, 'Driving'),
    (2, 'Idle'),
    (3, 'Stopped'),
    (5, 'Walking'),
    (6, 'Running'),
    (7, 'On Bicycle')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO mobile_device_motion_activity ("Id", "Name") VALUES
    (1, 'Still'),
    (2, 'Walking'),
    (3, 'On Foot'),
    (4, 'Running'),
    (5, 'On Bicycle'),
    (6, 'In Vehicle')
ON CONFLICT ("Id") DO NOTHING;
