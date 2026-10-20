using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using api_server.Controllers.Teams.Services;
using db_model.Gps;
using db_model.Map;
using db_model.Team;
using db_model.UserManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// Imitates the mobile client of a "Mock members" team and hunts for a race in
/// TeamContextService.GetTeamContextDeltaAsync: a delta that returns a GpsDeviceMapStates row for
/// a user the client has no Users / Members record for.
///
/// Every iteration is one full client session against a freshly reset database:
///   1. the creator (ApiFixture.TestUserId, "user 5" in the dev database) has a phone that already
///      reported a location (MockTeamMemberMove waits for it before it starts);
///   2. POST /api/team with notes "Mock members", POST /api/team/{id}/start;
///   3. GET /context, then GET /context/delta in a tight loop, keeping the cursor exactly like the
///      client does (only a 200 moves it, 204 leaves it);
///   4. MockTeamMemberMove switches the 4 mock users (UserSourceId 13, "users 7..10" in the dev
///      database) into the team one by one and keeps moving their map states;
///   5. the client state is applied delta by delta and checked after each one: every map state it
///      holds must have a matching User and Member. The iteration ends when all 4 mock users have
///      arrived as both Member and User.
/// The test stops at the first violation (or when a mock user never arrives) and fails with the
/// raw delta responses that led to it; otherwise it repeats up to MOCK_MEMBERS_ITERATIONS (100).
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Stress")]
public sealed class TeamContextMockMembersTests
{
    private const int MockMemberCount = 4;
    private const short MockUserSourceId = 13;
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan IterationTimeout = TimeSpan.FromSeconds(90);
    private const int RawResponsesInReport = 12;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public TeamContextMockMembersTests(ApiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact(Timeout = 1_800_000)]
    public async Task MockMembersTeam_DeltaNeverReturnsMapStateWithoutUserOrMember()
    {
        var iterations = int.TryParse(Environment.GetEnvironmentVariable("MOCK_MEMBERS_ITERATIONS"), out var n) && n > 0 ? n : 100;

        // Creating a team writes its join QR code into the media folder, which only exists once something uploaded.
        Directory.CreateDirectory(_fixture.MediaUploadFolder);

        // Same host as every other test, but the mock-member job runs on a 1 second beat instead of 10/30/5.
        await using var factory = _fixture.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<MockTeamMemberMoveOptions>(o =>
            {
                o.Enabled = true;
                o.CreatorPollSeconds = 1;
                o.SwitchDelaySeconds = 0;
                o.MoveIntervalSeconds = 1;
            })));

        for (var iteration = 1; iteration <= iterations; iteration++)
        {
            var problem = await RunClientSessionAsync(factory, iteration);
            if (problem != null)
            {
                Assert.Fail($"Iteration {iteration}/{iterations}: {problem}");
            }
        }
    }

    /// <summary>One client session; returns a description of what went wrong, or null when it was clean.</summary>
    private async Task<string?> RunClientSessionAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, int iteration)
    {
        await _fixture.ResetDatabaseAsync();
        var callerId = _fixture.TestUserId;

        var mockUserIds = new List<int>();
        for (var i = 0; i < MockMemberCount; i++) mockUserIds.Add(await CreateMockUserAsync(i + 1));

        // The creator's phone has already reported a location.
        var creatorDeviceId = await CreateDeviceAsync(callerId);
        await UpsertMapStateAsync(creatorDeviceId, callerId, -82.45, 27.95);

        var client = await LoginAsync(factory);

        var created = await client.PostAsJsonAsync("/api/team", new
        {
            team = new { teamName = "Mock team" },
            teamDetails = new { notes = "Mock members" },
        });
        if (created.StatusCode != HttpStatusCode.Created)
            Assert.Fail($"POST /api/team returned {(int)created.StatusCode}: {await created.Content.ReadAsStringAsync()}");
        var teamId = await created.Content.ReadFromJsonAsync<int>();
        var startResponse = await client.PostAsync($"/api/team/{teamId}/start", null);
        if (startResponse.StatusCode != HttpStatusCode.NoContent)
            Assert.Fail($"POST /api/team/{teamId}/start returned {(int)startResponse.StatusCode}: {await startResponse.Content.ReadAsStringAsync()}");

        var full = await client.GetAsync($"/api/team/{teamId}/context");
        full.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fullBody = (await full.Content.ReadFromJsonAsync<ContextResponse>(Json))!;
        var cursor = fullBody.LastUpdate!.Value;

        var state = new ClientState();
        state.ApplyFull(fullBody);
        var history = new List<string>();
        var deltas = 0;
        var started = DateTime.UtcNow;

        while (!state.HasAllMembersAndUsers(mockUserIds))
        {
            if (DateTime.UtcNow - started > IterationTimeout)
            {
                return $"timed out after {IterationTimeout.TotalSeconds:0}s: client has members [{string.Join(",", state.Members.Keys)}] and users [{string.Join(",", state.Users.Keys)}], " +
                       $"expected {string.Join(",", mockUserIds)}\n{Report(history)}";
            }

            await Task.Delay(PollDelay);
            var response = await client.GetAsync($"/api/team/{teamId}/context/delta?latestUpdateDate={cursor}");
            if (response.StatusCode == HttpStatusCode.NoContent) continue; // the cursor stays put
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return $"delta returned {(int)response.StatusCode} for cursor {cursor}\n{Report(history)}";
            }

            var raw = await response.Content.ReadAsStringAsync();
            history.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] since={cursor} {raw}");
            var delta = JsonSerializer.Deserialize<ContextResponse>(raw, Json)!;
            deltas++;

            state.ApplyDelta(delta);
            var violations = state.FindMapStatesWithoutUserOrMember();
            if (violations.Count > 0)
            {
                return $"GpsDeviceMapStates without matching User/Member after delta #{deltas} (requested since={cursor}):\n  " +
                       string.Join("\n  ", violations) + "\n" + await DescribeTimestampsAsync(teamId, cursor) + Report(history);
            }

            cursor = delta.LastUpdate!.Value;
        }

        _output.WriteLine($"iteration {iteration}: clean, all {MockMemberCount} mock users arrived after {deltas} non-empty deltas in {(DateTime.UtcNow - started).TotalSeconds:0.0}s");
        return null;
    }

    /// <summary>The clocks and row timestamps that decide what a delta with this cursor includes.</summary>
    private Task<string> DescribeTimestampsAsync(int teamId, long cursor) => _fixture.QueryDbAsync(async db =>
    {
        var sb = new StringBuilder();
        var dbNow = (await db.Database.SqlQueryRaw<DateTime>("select now() as \"Value\"").ToListAsync()).Single();
        sb.AppendLine($"cursor (since) = {DateTimeOffset.FromUnixTimeMilliseconds(cursor):HH:mm:ss.fff}Z; DB now() = {dbNow:HH:mm:ss.fff}; app UtcNow = {DateTime.UtcNow:HH:mm:ss.fff}");
        foreach (var m in await db.Set<TeamMemberUser>().AsNoTracking().Where(m => m.TeamId == teamId).ToListAsync())
        {
            var user = await db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == m.UserId);
            var state = await db.Set<MobileGpsDeviceMapState>().AsNoTracking().Where(s => s.UserId == m.UserId).ToListAsync();
            sb.AppendLine($"  user {m.UserId}: member.LatestUpdate={m.LatestUpdate:HH:mm:ss.fff} (status {m.StatusId}), user.LatestUpdate={user.LatestUpdate:HH:mm:ss.fff}, " +
                          $"mapState.LatestOnMapUpdate=[{string.Join(", ", state.Select(s => s.LatestOnMapUpdate.ToString("HH:mm:ss.fff")))}]");
        }
        return sb.ToString();
    });

    private static string Report(List<string> history)
    {
        var sb = new StringBuilder("last delta responses (oldest first):\n");
        foreach (var line in history.TakeLast(RawResponsesInReport)) sb.AppendLine(line);
        return sb.ToString();
    }

    private static async Task<HttpClient> LoginAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<ApiFixture.LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    // --- database helpers ---------------------------------------------------------

    private Task<int> CreateMockUserAsync(int number) => _fixture.QueryDbAsync(async db =>
    {
        var user = new User
        {
            UserName = $"mock.user{number}.{Guid.NewGuid().ToString("N")[..8]}",
            DisplayName = $"Mock {number}",
            FullName = $"Mock {number}",
            Password = "password",
            StatusId = UserStatus.Active,
            UserSourceId = MockUserSourceId,
        };
        db.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    });

    private Task<int> CreateDeviceAsync(int userId) => _fixture.QueryDbAsync(async db =>
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

    private Task UpsertMapStateAsync(int deviceId, int userId, double longitude, double latitude) => _fixture.QueryDbAsync(async db =>
    {
        db.Add(new MobileGpsDeviceMapState
        {
            MobileGpsDeviceId = deviceId,
            UserId = userId,
            Location = new Point(longitude, latitude),
            LatestOnMapUpdate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return true;
    });

    // --- the client ------------------------------------------------------------------

    /// <summary>What the mobile client keeps from the team context, updated the way it applies a delta.</summary>
    private sealed class ClientState
    {
        public Dictionary<int, MemberRecord> Members { get; } = new();
        public Dictionary<int, UserRecord> Users { get; } = new();
        public Dictionary<int, MapStateRecord> MapStatesByDevice { get; } = new();

        /// <summary>DeviceIds of the map states that arrived in the delta applied last.</summary>
        private HashSet<int> _lastDeltaDevices = new();

        public void ApplyFull(ContextResponse full)
        {
            foreach (var m in full.Members ?? []) Members[m.UserId] = m;
            foreach (var u in full.Users ?? []) Users[u.Id] = u;
            foreach (var s in full.GpsDeviceMapStates ?? []) MapStatesByDevice[s.MobileGpsDeviceId] = s;
        }

        public void ApplyDelta(ContextResponse delta)
        {
            // Removals first: a member that is "loaded again" comes with a removal marker and fresh records.
            foreach (var userId in delta.RemoveMemberUserIds ?? [])
            {
                Members.Remove(userId);
                RemoveMapStatesOf(userId);
            }
            foreach (var userId in delta.RemoveUserIds ?? [])
            {
                Users.Remove(userId);
                Members.Remove(userId);
                RemoveMapStatesOf(userId);
            }

            foreach (var m in delta.Members ?? []) Members[m.UserId] = m;
            foreach (var u in delta.Users ?? []) Users[u.Id] = u;
            _lastDeltaDevices = new HashSet<int>();
            foreach (var s in delta.GpsDeviceMapStates ?? [])
            {
                MapStatesByDevice[s.MobileGpsDeviceId] = s;
                _lastDeltaDevices.Add(s.MobileGpsDeviceId);
            }
        }

        public bool HasAllMembersAndUsers(IEnumerable<int> userIds) =>
            userIds.All(id => Members.ContainsKey(id) && Users.ContainsKey(id));

        public List<string> FindMapStatesWithoutUserOrMember()
        {
            var violations = new List<string>();
            foreach (var s in MapStatesByDevice.Values)
            {
                var missing = new List<string>();
                if (!Users.ContainsKey(s.UserId)) missing.Add("User");
                if (!Members.ContainsKey(s.UserId)) missing.Add("Member");
                if (missing.Count == 0) continue;
                var origin = _lastDeltaDevices.Contains(s.MobileGpsDeviceId) ? "returned in this delta" : "kept from an earlier delta";
                violations.Add($"map state of device {s.MobileGpsDeviceId} (user {s.UserId}, {origin}) has no {string.Join(" and ", missing)}");
            }
            return violations;
        }

        private void RemoveMapStatesOf(int userId)
        {
            foreach (var deviceId in MapStatesByDevice.Where(p => p.Value.UserId == userId).Select(p => p.Key).ToList())
                MapStatesByDevice.Remove(deviceId);
        }
    }

    private sealed record ContextResponse(
        long? LastUpdate,
        List<MemberRecord>? Members,
        List<int>? RemoveMemberUserIds,
        List<UserRecord>? Users,
        List<int>? RemoveUserIds,
        List<MapStateRecord>? GpsDeviceMapStates);

    private sealed record MemberRecord(int UserId, short RoleId);
    private sealed record UserRecord(int Id, string? DisplayName);
    private sealed record MapStateRecord(int MobileGpsDeviceId, int UserId, double Latitude, double Longitude, long LatestOnMapUpdate);
}
