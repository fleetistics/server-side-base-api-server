using System.Net;
using System.Net.Http.Json;
using db_model.Gps;
using db_model.Map;
using db_model.Media;
using db_model.Team;
using db_model.UserManagement;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// Covers TeamContextController against the scenarios documented in
/// api-server/docs/logic/TeamContextDelta.txt: GET .../context (full snapshot) and GET
/// .../context/delta?latestUpdateDate=... (only what changed) are two separate endpoints -
/// every kind of change surfaced in a delta, the no-changes-since-last-sync 204 response, and
/// the 403 the caller gets when it isn't currently an active member of the team, on both
/// endpoints. CreateTeamAsync adds the authenticated test user (ApiFixture.TestUserId - the
/// caller for every request here) as an active member by default so the other scenarios don't
/// need to think about this; the caller is expected to never appear in the response bodies
/// regardless (it isn't told about itself).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TeamContextApiTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;

    public TeamContextApiTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetTeamContext_WithoutToken_Returns401()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/team/999999/context");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTeamContext_UnknownTeam_Returns410()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/team/999999/context");

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task GetTeamContextDelta_UnknownTeam_Returns410()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/team/999999/context/delta?latestUpdateDate=0");

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task GetTeamContext_InactiveTeam_Returns410()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync(statusId: TeamStatus.Completed);

        var response = await client.GetAsync($"/api/team/{teamId}/context");

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task GetTeamContextDelta_InactiveTeam_Returns410()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync(statusId: TeamStatus.Completed);

        var response = await client.GetAsync($"/api/team/{teamId}/context/delta?latestUpdateDate=0");

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task GetTeamContextDelta_MissingLatestUpdateDate_Returns400()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();

        var response = await client.GetAsync($"/api/team/{teamId}/context/delta");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTeamContext_CallerNeverMember_FullLoad_Returns403()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync(addCallerAsMember: false);

        var response = await client.GetAsync($"/api/team/{teamId}/context");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTeamContext_CallerNeverMember_Delta_Returns403()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync(addCallerAsMember: false);

        var response = await client.GetAsync($"/api/team/{teamId}/context/delta?latestUpdateDate=0");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTeamContext_CallerMemberButNotActive_FullLoad_Returns403()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        await SetMemberStatusAsync(teamId, _fixture.TestUserId, TeamMemberUserStatus.Left);

        var response = await client.GetAsync($"/api/team/{teamId}/context");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTeamContext_CallerMemberButNotActive_Delta_Returns403()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        await SetMemberStatusAsync(teamId, _fixture.TestUserId, TeamMemberUserStatus.Left);

        var response = await client.GetAsync($"/api/team/{teamId}/context/delta?latestUpdateDate=0");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTeamContext_FullLoad_ReturnsTeamAndActiveVisibleMembersOnly()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync(notes: "hello team");
        var visibleUserId = await CreateUserAsync("visible.member");
        var privateUserId = await CreateUserAsync("private.member");
        var inactiveUserId = await CreateUserAsync("inactive.member");
        await SetUserStatusAsync(inactiveUserId, UserStatus.Inactive);
        await AddMemberAsync(teamId, visibleUserId);
        await AddMemberAsync(teamId, privateUserId);
        await AddMemberAsync(teamId, inactiveUserId);
        await SetPrivacyAsync(privateUserId, privacyMode: 1);
        await CreateAlertAsync(visibleUserId);
        var deviceId = await CreateDeviceAsync(visibleUserId);
        await UpsertMapStateAsync(deviceId, visibleUserId, -82.45, 27.95, DateTime.UtcNow);

        var response = await client.GetAsync($"/api/team/{teamId}/context");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TeamContextResponse>();
        body!.LastUpdate.ShouldNotBeNull();
        body.TeamHeader.ShouldNotBeNull();
        body.TeamDetails.ShouldNotBeNull();
        body.TeamDetails!.Notes.ShouldBe("hello team");

        body.Members!.Select(m => m.UserId).ShouldBe(new[] { visibleUserId });
        body.Users!.Select(u => u.Id).ShouldBe(new[] { visibleUserId });
        body.MobileGpsDevices!.Select(d => d.UserId).ShouldBe(new[] { visibleUserId });
        body.GpsDeviceMapStates!.Select(s => s.MobileGpsDeviceId).ShouldBe(new[] { deviceId });
        body.UserAlerts!.Select(a => a.UserId).ShouldBe(new[] { visibleUserId });
    }

    [Fact]
    public async Task GetTeamContext_NothingChangedSinceLastSync_Returns204()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        var userId = await CreateUserAsync("steady.user");
        await AddMemberAsync(teamId, userId);
        var cursor = await FullLoadCursorAsync(client, teamId);

        var response = await client.GetAsync($"/api/team/{teamId}/context/delta?latestUpdateDate={cursor}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetTeamContext_TeamDetailsChanged_ReturnsTeamAndThenSettles()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        var cursor = await FullLoadCursorAsync(client, teamId);

        await SetTeamNotesAsync(teamId, "updated notes");

        var (status, body) = await GetContextAsync(client, teamId, cursor);
        status.ShouldBe(HttpStatusCode.OK);
        body!.TeamDetails.ShouldNotBeNull();
        body.TeamDetails!.Notes.ShouldBe("updated notes");
        body.TeamHeader.ShouldBeNull(); // only Notes changed - the header itself didn't

        await AssertSettlesAsync(client, teamId, body.LastUpdate!.Value);
    }

    [Fact]
    public async Task GetTeamContext_TeamMediaAdded_ReturnsMediaAndThenSettles()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        var cursor = await FullLoadCursorAsync(client, teamId);

        var mediaId = await AddTeamMediaAsync(teamId);

        var (status, body) = await GetContextAsync(client, teamId, cursor);
        status.ShouldBe(HttpStatusCode.OK);
        body!.TeamUploadedMedias!.Select(m => m.Id).ShouldContain(mediaId);

        await AssertSettlesAsync(client, teamId, body.LastUpdate!.Value);
    }

    [Fact]
    public async Task GetTeamContext_TeamMediaRemoved_ReturnsRemoveTeamMediaIdAndThenSettles()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        var mediaId = await AddTeamMediaAsync(teamId);
        var cursor = await FullLoadCursorAsync(client, teamId);

        await RemoveTeamMediaAsync(teamId, mediaId);

        var (status, body) = await GetContextAsync(client, teamId, cursor);
        status.ShouldBe(HttpStatusCode.OK);
        body!.RemoveTeamMediaIds.ShouldContain(mediaId);
        (body.TeamUploadedMedias ?? []).ShouldNotContain(m => m.Id == mediaId);

        await AssertSettlesAsync(client, teamId, body.LastUpdate!.Value);
    }

    [Fact]
    public async Task GetTeamContext_NewMemberAdded_ReturnsMemberAndUserAndThenSettles()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        var cursor = await FullLoadCursorAsync(client, teamId);

        var newUserId = await CreateUserAsync("new.member");
        await AddMemberAsync(teamId, newUserId);

        var (status, body) = await GetContextAsync(client, teamId, cursor);
        status.ShouldBe(HttpStatusCode.OK);
        body!.Members!.Select(m => m.UserId).ShouldContain(newUserId);
        body.Users!.Select(u => u.Id).ShouldContain(newUserId);

        await AssertSettlesAsync(client, teamId, body.LastUpdate!.Value);
    }

    [Fact]
    public async Task GetTeamContext_MemberLeaves_ReturnsRemoveUserIdAndThenSettles()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        var userId = await CreateUserAsync("leaving.member");
        await AddMemberAsync(teamId, userId);
        var cursor = await FullLoadCursorAsync(client, teamId);

        await SetMemberStatusAsync(teamId, userId, TeamMemberUserStatus.Left);

        var (status, body) = await GetContextAsync(client, teamId, cursor);
        status.ShouldBe(HttpStatusCode.OK);
        body!.RemoveUserIds.ShouldContain(userId);

        await AssertSettlesAsync(client, teamId, body.LastUpdate!.Value);
    }

    [Fact]
    public async Task GetTeamContext_MemberUserProfileUpdatedOnly_StillReturnsUpdatedUserAndThenSettles()
    {
        // Regression test: this is the scenario a bug in an earlier draft of
        // TeamContextDelta.txt (a stray "PrivacyMode = 0 -> skip" guard) short-circuited
        // before ever checking whether the User row itself had changed.
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        var userId = await CreateUserAsync("profile.update.member");
        await AddMemberAsync(teamId, userId);
        var cursor = await FullLoadCursorAsync(client, teamId);

        await PatchDisplayNameAsync(userId, "New Display Name");

        var (status, body) = await GetContextAsync(client, teamId, cursor);
        status.ShouldBe(HttpStatusCode.OK);
        body!.Users!.ShouldContain(u => u.Id == userId && u.DisplayName == "New Display Name");

        await AssertSettlesAsync(client, teamId, body.LastUpdate!.Value);
    }

    [Fact]
    public async Task GetTeamContext_SteadyMemberGetsNewAlert_ReturnsAlertViaBatchQueryAndThenSettles()
    {
        // Member/User/Privacy are all unchanged - only a brand-new alert appears. This only
        // reaches TeamContextDelta.UserAlerts through the teamUserIds batch load (spec step
        // 68), not through fullSingleUserLoadload.
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        var userId = await CreateUserAsync("steady.alert.member");
        await AddMemberAsync(teamId, userId);
        var cursor = await FullLoadCursorAsync(client, teamId);

        var alertId = await CreateAlertAsync(userId);

        var (status, body) = await GetContextAsync(client, teamId, cursor);
        status.ShouldBe(HttpStatusCode.OK);
        body!.UserAlerts!.ShouldContain(a => a.UserId == userId);
        (body.Users ?? []).ShouldNotContain(u => u.Id == userId); // no unrelated user reload triggered

        await AssertSettlesAsync(client, teamId, body.LastUpdate!.Value);
        _ = alertId;
    }

    [Fact]
    public async Task GetTeamContext_EmergencyAlertCompleted_ReturnsRemoveUserAlertIdAndThenSettles()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        var userId = await CreateUserAsync("alert.completes.member");
        await AddMemberAsync(teamId, userId);
        var alertId = await CreateAlertAsync(userId);
        var cursor = await FullLoadCursorAsync(client, teamId);

        await CompleteAlertAsync(alertId);

        var (status, body) = await GetContextAsync(client, teamId, cursor);
        status.ShouldBe(HttpStatusCode.OK);
        body!.RemoveUserAlertIds.ShouldContain(alertId);
        (body.UserAlerts ?? []).ShouldNotContain(a => a.UserId == userId);

        await AssertSettlesAsync(client, teamId, body.LastUpdate!.Value);
    }

    [Fact]
    public async Task GetTeamContext_SteadyMemberGetsNewMapState_ReturnsMapStateViaBatchQueryAndThenSettles()
    {
        // Member/User/Privacy are all unchanged - only the GPS map state moves. This only
        // reaches TeamContextDelta.GpsDeviceMapStates through the teamUserIds batch load
        // (spec step 69 - `UserId in teamUserIds`, not the single `userId` typo it once had).
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        var userId = await CreateUserAsync("steady.mapstate.member");
        await AddMemberAsync(teamId, userId);
        var deviceId = await CreateDeviceAsync(userId);
        await UpsertMapStateAsync(deviceId, userId, -82.0, 27.0, DateTime.UtcNow.AddMinutes(-10));
        var cursor = await FullLoadCursorAsync(client, teamId);

        await UpsertMapStateAsync(deviceId, userId, -82.5, 27.5, DateTime.UtcNow);

        var (status, body) = await GetContextAsync(client, teamId, cursor);
        status.ShouldBe(HttpStatusCode.OK);
        body!.GpsDeviceMapStates!.ShouldContain(s => s.MobileGpsDeviceId == deviceId);
        (body.Users ?? []).ShouldNotContain(u => u.Id == userId);

        await AssertSettlesAsync(client, teamId, body.LastUpdate!.Value);
    }

    [Fact]
    public async Task GetTeamContext_UserOptsIntoLocationPrivacy_ReturnsRemoveUserIdAndThenSettles()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        var userId = await CreateUserAsync("goes.private.member");
        await AddMemberAsync(teamId, userId);
        var cursor = await FullLoadCursorAsync(client, teamId);

        await SetPrivacyAsync(userId, privacyMode: 1);

        var (status, body) = await GetContextAsync(client, teamId, cursor);
        status.ShouldBe(HttpStatusCode.OK);
        body!.RemoveUserIds.ShouldContain(userId);

        await AssertSettlesAsync(client, teamId, body.LastUpdate!.Value);
    }

    [Fact]
    public async Task GetTeamContext_UserOptsOutOfLocationPrivacy_ReturnsFullUserReloadWithoutMemberRow()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        var userId = await CreateUserAsync("goes.public.member");
        await AddMemberAsync(teamId, userId);
        await SetPrivacyAsync(userId, privacyMode: 1);
        // The user was already hidden as of the very first snapshot the client saw.
        var cursor = await FullLoadCursorAsync(client, teamId);

        await SetPrivacyAsync(userId, privacyMode: 0);

        var (status, body) = await GetContextAsync(client, teamId, cursor);
        status.ShouldBe(HttpStatusCode.OK);
        body!.Users!.Select(u => u.Id).ShouldContain(userId);
        // The membership row itself never changed, so per spec it is not re-sent here -
        // the client is expected to already not have this user at all yet.
        (body.Members ?? []).ShouldNotContain(m => m.UserId == userId);

        await AssertSettlesAsync(client, teamId, body.LastUpdate!.Value);
    }

    [Fact]
    public async Task GetTeamContext_UserBecomesInactive_ReturnsRemoveUserIdAndThenSettles()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var teamId = await CreateTeamAsync();
        var userId = await CreateUserAsync("deactivated.member");
        await AddMemberAsync(teamId, userId);
        var cursor = await FullLoadCursorAsync(client, teamId);

        await SetUserStatusAsync(userId, UserStatus.Inactive);

        var (status, body) = await GetContextAsync(client, teamId, cursor);
        status.ShouldBe(HttpStatusCode.OK);
        body!.RemoveUserIds.ShouldContain(userId);

        await AssertSettlesAsync(client, teamId, body.LastUpdate!.Value);
    }

    // --- helpers -----------------------------------------------------------------

    /// <summary>
    /// Fetches the full snapshot and returns its LastUpdate cursor for a follow-up delta call.
    /// Waits afterward so a change made right after this call is unambiguously past the
    /// returned cursor: LastUpdate is rounded up to a whole unix second on the wire (see the
    /// comment on TeamContextService.GetTeamContextUpdateAsync), which guarantees baseline
    /// records never reappear but means a change landing within that same rounded second could
    /// otherwise be missed until the following poll.
    /// </summary>
    private async Task<long> FullLoadCursorAsync(HttpClient client, int teamId)
    {
        var response = await client.GetAsync($"/api/team/{teamId}/context");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TeamContextResponse>();
        body!.LastUpdate.ShouldNotBeNull();
        await Task.Delay(TimeSpan.FromSeconds(1.2));
        return body.LastUpdate!.Value;
    }

    private async Task<(HttpStatusCode Status, TeamContextResponse? Body)> GetContextAsync(HttpClient client, int teamId, long since)
    {
        var response = await client.GetAsync($"/api/team/{teamId}/context/delta?latestUpdateDate={since}");
        var body = response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<TeamContextResponse>()
            : null;
        return (response.StatusCode, body);
    }

    /// <summary>Polling again with the delta's own returned cursor must settle to 204 - the
    /// record(s) just reported must never be reported again on the very next call (see the
    /// DateTime.UtcNow-inside-the-query comment on TeamContextService.loadFullAsync).</summary>
    private async Task AssertSettlesAsync(HttpClient client, int teamId, long cursor)
    {
        var response = await client.GetAsync($"/api/team/{teamId}/context/delta?latestUpdateDate={cursor}");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Creates a team and, by default, makes ApiFixture.TestUserId (the caller for every
    /// request in this file) an active member of it - required since GetTeamContextUpdate now
    /// 403s any caller who isn't. Pass addCallerAsMember: false for the tests that specifically
    /// exercise that 403.
    /// </summary>
    private async Task<int> CreateTeamAsync(short statusId = TeamStatus.Active, string? notes = null, bool addCallerAsMember = true)
    {
        var teamId = await _fixture.QueryDbAsync(async db =>
        {
            var team = new Team
            {
                CreatorUserId = _fixture.TestUserId,
                TeamName = "Test Team",
                StatusId = statusId,
                CreatedDate = DateTime.UtcNow,
                JoinKey = Guid.NewGuid().ToString("N")[..6],
            };
            db.Add(team);
            await db.SaveChangesAsync();

            db.Add(new TeamDetails { TeamId = team.Id, Notes = notes });
            await db.SaveChangesAsync();

            return team.Id;
        });

        if (addCallerAsMember)
        {
            await AddMemberAsync(teamId, _fixture.TestUserId);
        }

        return teamId;
    }

    private async Task SetTeamNotesAsync(int teamId, string notes)
    {
        await _fixture.QueryDbAsync(async db =>
        {
            var details = await db.Set<TeamDetails>().SingleAsync(d => d.TeamId == teamId);
            details.Notes = notes;
            await db.SaveChangesAsync();
            return true;
        });
    }

    private async Task<int> CreateUserAsync(string name)
    {
        return await _fixture.QueryDbAsync(async db =>
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var user = new User
            {
                UserName = $"{name}.{suffix}",
                DisplayName = name,
                FullName = name,
                Password = "password",
                StatusId = UserStatus.Active,
                UserSourceId = 1,
            };
            db.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        });
    }

    private async Task AddMemberAsync(int teamId, int userId, short statusId = TeamMemberUserStatus.Active)
    {
        await _fixture.QueryDbAsync(async db =>
        {
            db.Add(new TeamMemberUser { TeamId = teamId, UserId = userId, StatusId = statusId, RoleId = 1 });
            await db.SaveChangesAsync();
            return true;
        });
    }

    private async Task SetMemberStatusAsync(int teamId, int userId, short statusId)
    {
        await _fixture.QueryDbAsync(async db =>
        {
            var member = await db.Set<TeamMemberUser>().SingleAsync(m => m.TeamId == teamId && m.UserId == userId);
            member.StatusId = statusId;
            await db.SaveChangesAsync();
            return true;
        });
    }

    private async Task SetPrivacyAsync(int userId, short privacyMode)
    {
        await _fixture.QueryDbAsync(async db =>
        {
            var privacy = await db.Set<UserLocationPrivacy>().SingleOrDefaultAsync(p => p.UserId == userId);
            if (privacy == null)
            {
                db.Add(new UserLocationPrivacy { UserId = userId, PrivacyMode = privacyMode });
            }
            else
            {
                privacy.PrivacyMode = privacyMode;
            }
            await db.SaveChangesAsync();
            return true;
        });
    }

    private async Task SetUserStatusAsync(int userId, short statusId)
    {
        await _fixture.QueryDbAsync(async db =>
        {
            var user = await db.Set<User>().SingleAsync(u => u.Id == userId);
            user.StatusId = statusId;
            await db.SaveChangesAsync();
            return true;
        });
    }

    private async Task PatchDisplayNameAsync(int userId, string displayName)
    {
        await _fixture.QueryDbAsync(async db =>
        {
            var user = await db.Set<User>().SingleAsync(u => u.Id == userId);
            user.DisplayName = displayName;
            await db.SaveChangesAsync();
            return true;
        });
    }

    private async Task<int> AddTeamMediaAsync(int teamId, short? groupKey = null)
    {
        return await _fixture.QueryDbAsync(async db =>
        {
            var media = new UploadedMedia { FileName = "team-photo.png", MediaType = 1, Guid = Guid.NewGuid().ToString() };
            db.Add(media);
            await db.SaveChangesAsync();

            db.Add(new TeamUploadedMedia { TeamId = teamId, UploadedMediaId = media.Id, GroupKey = groupKey });
            await db.SaveChangesAsync();
            return media.Id;
        });
    }

    private async Task RemoveTeamMediaAsync(int teamId, int uploadedMediaId)
    {
        await _fixture.QueryDbAsync(async db =>
        {
            var teamMedia = await db.Set<TeamUploadedMedia>().SingleAsync(m => m.TeamId == teamId && m.UploadedMediaId == uploadedMediaId);
            teamMedia.GroupKey = -1;
            await db.SaveChangesAsync();
            return true;
        });
    }

    private async Task<int> CreateAlertAsync(int userId)
    {
        return await _fixture.QueryDbAsync(async db =>
        {
            var alert = new UserEmergencyAlert
            {
                UserId = userId,
                CreatedDate = DateTime.UtcNow,
                StatusId = UserEmergencyAlertStatus.Active,
            };
            db.Add(alert);
            await db.SaveChangesAsync();
            return alert.Id;
        });
    }

    private async Task CompleteAlertAsync(int alertId)
    {
        await _fixture.QueryDbAsync(async db =>
        {
            var alert = await db.Set<UserEmergencyAlert>().SingleAsync(a => a.Id == alertId);
            alert.StatusId = UserEmergencyAlertStatus.ColosedByUser;
            await db.SaveChangesAsync();
            return true;
        });
    }

    private async Task<int> CreateDeviceAsync(int userId)
    {
        return await _fixture.QueryDbAsync(async db =>
        {
            var device = new MobileGpsDevice
            {
                ProviderId = 1,
                StatusId = 1,
                DeviceUID = Guid.NewGuid().ToString("N"),
                UserId = userId,
            };
            db.Add(device);
            await db.SaveChangesAsync();
            return device.Id;
        });
    }

    private async Task UpsertMapStateAsync(int deviceId, int userId, double longitude, double latitude, DateTime latestOnMapUpdate)
    {
        await _fixture.QueryDbAsync(async db =>
        {
            var state = await db.Set<MobileGpsDeviceMapState>().SingleOrDefaultAsync(s => s.MobileGpsDeviceId == deviceId);
            if (state == null)
            {
                db.Add(new MobileGpsDeviceMapState
                {
                    MobileGpsDeviceId = deviceId,
                    UserId = userId,
                    Location = new Point(longitude, latitude),
                    LatestOnMapUpdate = latestOnMapUpdate,
                });
            }
            else
            {
                state.Location = new Point(longitude, latitude);
                state.LatestOnMapUpdate = latestOnMapUpdate;
            }
            await db.SaveChangesAsync();
            return true;
        });
    }

    private sealed record TeamContextResponse(
        long? LastUpdate,
        TeamHeaderRecord? TeamHeader,
        TeamDetailsRecord? TeamDetails,
        List<MediaRecord>? TeamUploadedMedias,
        List<MemberRecord>? Members,
        List<UserRecord>? Users,
        List<DeviceRecord>? MobileGpsDevices,
        List<MapStateRecord>? GpsDeviceMapStates,
        List<AlertRecord>? UserAlerts,
        List<int>? RemoveUserIds,
        List<int>? RemoveUserAlertIds,
        List<int>? RemoveTeamMediaIds);

    private sealed record TeamHeaderRecord(int? Id, int CreatorUserId, string TeamName, short? StatusId);
    private sealed record TeamDetailsRecord(string? Notes);
    private sealed record MemberRecord(int UserId, short RoleId);
    private sealed record UserRecord(int Id, string DisplayName, string? UserName, string? FullName);
    private sealed record MediaRecord(int Id, string Url, short? GroupKey);
    private sealed record DeviceRecord(int Id, int UserId, string? Name);
    private sealed record MapStateRecord(int MobileGpsDeviceId, double Latitude, double Longitude, long LatestOnMapUpdate);
    private sealed record AlertRecord(int UserId, long CreatedDate);
}
