using System.Net;
using System.Net.Http.Json;
using api_server.core;
using db_model.AppStructure;
using db_model.Companies;
using db_model.UserManagement;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// /api/admin/roles: creating, renaming, duplicating and deleting roles, the company's users, and giving users a role (RolesAdminController).
/// Portal Admin only. The schema comes from the model (EnsureCreated), so the tests seed what they need.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RolesAdminControllerTests : IAsyncLifetime
{
    private const string Prefix = "/api/admin/roles";

    private readonly ApiFixture _fixture;

    public RolesAdminControllerTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed record RoleRow(int Id, string Name, string? Description, bool IsSystem, int MemberCount, int ViewCount, int EditCount);
    private sealed record UserRow(int UserId, string DisplayName, string? Email, short StatusId, int RoleId, bool IsCurrentUser);

    private Task<int> AddRoleAsync(string name, int? companyId = null, string? description = null) => _fixture.QueryDbAsync(async db =>
    {
        var role = new PermissionRole { Name = name, CompanyId = companyId, Description = description };
        db.Set<PermissionRole>().Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    });

    private Task<int> AddMenuItemAsync(string title) => _fixture.QueryDbAsync(async db =>
    {
        var item = new ApplicationMenuItem { MenuId = 1, Title = title, Path = "/" + title.ToLowerInvariant() };
        db.Set<ApplicationMenuItem>().Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    });

    private Task AddGrantAsync(int roleId, int itemId, bool edit = false) => _fixture.QueryDbAsync(async db =>
    {
        db.Set<Role2MenuItem>().Add(new Role2MenuItem { PermissionRoleId = roleId, ApplicationMenuItemId = itemId, CanView = true, CanEdit = edit, CanCreate = false, CanDelete = false });
        await db.SaveChangesAsync();
        return 0;
    });

    private Task<List<Role2MenuItem>> GrantsOfAsync(int roleId) =>
        _fixture.QueryDbAsync(db => db.Set<Role2MenuItem>().Where(g => g.PermissionRoleId == roleId).AsNoTracking().OrderBy(g => g.ApplicationMenuItemId).ToListAsync());

    /// <summary>Another user who belongs to the company with the role.</summary>
    private Task<int> AddMemberAsync(int companyId, int roleId, string displayName, short status = User2CompanyStatus.Active) => _fixture.QueryDbAsync(async db =>
    {
        var user = new User
        {
            UserName = displayName.ToLowerInvariant().Replace(' ', '.'),
            Password = "x",
            DisplayName = displayName,
            FullName = displayName,
            Email = displayName.ToLowerInvariant().Replace(' ', '.') + "@example.test",
            Phone = "8135550101",
            StatusId = UserStatus.Active,
        };
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();
        db.Set<User2Company>().Add(new User2Company { CompanyId = companyId, UserId = user.Id, PermissionRoleId = roleId, StatusId = status, JoinTypeId = 1, JoinedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return user.Id;
    });

    private Task<int> RoleOfAsync(int companyId, int userId) =>
        _fixture.QueryDbAsync(db => db.Set<User2Company>().Where(m => m.CompanyId == companyId && m.UserId == userId).Select(m => m.PermissionRoleId).SingleAsync());

    // ---- who may call ----

    [Fact]
    public async Task TheNewEndpoints_ForAnAdministratorWhoIsNotAPortalAdmin_AreAll403_AndChangeNothing()
    {
        var (client, companyId) = await PortalAdminTestClients.CreateAsync(_fixture, "Administrator");
        var own = await AddRoleAsync("Ours", companyId);

        (await client.PostAsJsonAsync(Prefix, new { Name = "New" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PatchAsJsonAsync($"{Prefix}/{own}", new { Name = "Renamed" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.DeleteAsync($"{Prefix}/{own}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync($"{Prefix}/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync($"{Prefix}/{own}/members", new { UserIds = new[] { 1 } })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().Where(r => r.CompanyId == companyId).Select(r => r.Name).ToListAsync())).ShouldBe(["Ours"]);
    }

    [Fact]
    public async Task TheNewEndpoints_WithoutALogin_Are401()
    {
        var anonymous = _fixture.CreateClient();

        (await anonymous.PostAsJsonAsync(Prefix, new { Name = "New" })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PatchAsJsonAsync($"{Prefix}/1", new { Name = "Renamed" })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync($"{Prefix}/1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"{Prefix}/users")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync($"{Prefix}/1/members", new { UserIds = new[] { 1 } })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- create ----

    [Fact]
    public async Task Create_MakesARoleOfTheCallersCompany_WithATrimmedNameAndNoAccessRights()
    {
        var (client, companyId) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);

        var response = await client.PostAsJsonAsync(Prefix, new { Name = "  Regional Dispatcher ", Description = " Dispatches a region " });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<RoleRow>();
        created!.Name.ShouldBe("Regional Dispatcher");
        created.Description.ShouldBe("Dispatches a region");
        created.IsSystem.ShouldBeFalse();
        created.MemberCount.ShouldBe(0);
        created.ViewCount.ShouldBe(0);
        var stored = await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().SingleAsync(r => r.Id == created.Id));
        stored.CompanyId.ShouldBe(companyId);
        (await GrantsOfAsync(created.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_WithARoleToCopy_StartsWithItsAccessRights_AndLeavesTheSourceAlone()
    {
        var (client, _) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var a = await AddMenuItemAsync("Jobs");
        var b = await AddMenuItemAsync("Tasks");
        var source = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(source, a, edit: true);
        await AddGrantAsync(source, b);

        var response = await client.PostAsJsonAsync(Prefix, new { Name = "Dispatcher copy", CopyFromRoleId = source });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<RoleRow>();
        created!.ViewCount.ShouldBe(2);
        created.EditCount.ShouldBe(1);
        var copied = await GrantsOfAsync(created.Id);
        copied.Select(g => (g.ApplicationMenuItemId, g.CanView, g.CanEdit)).ShouldBe([(a, true, true), (b, true, false)]);
        (await GrantsOfAsync(source)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Create_WithABlankName_Is400()
    {
        var (client, _) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);

        (await client.PostAsJsonAsync(Prefix, new { Name = "   " })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(Prefix, new { Name = new string('x', 101) })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithANameThatIsTaken_IgnoringCase_Is409()
    {
        var (client, _) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        await AddRoleAsync("Dispatcher");

        (await client.PostAsJsonAsync(Prefix, new { Name = "dispatcher" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithARoleToCopyThatTheCompanyCannotUse_Is400_AndCreatesNothing()
    {
        var (client, _) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var otherCompany = await _fixture.QueryDbAsync(async db =>
        {
            var company = new Company { Name = "Other", StatusId = 1 };
            db.Set<Company>().Add(company);
            await db.SaveChangesAsync();
            return company.Id;
        });
        var theirs = await AddRoleAsync("Theirs", otherCompany);

        (await client.PostAsJsonAsync(Prefix, new { Name = "Mine", CopyFromRoleId = theirs })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(Prefix, new { Name = "Mine", CopyFromRoleId = 999999 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().AnyAsync(r => r.Name == "Mine"))).ShouldBeFalse();
    }

    // ---- rename ----

    [Fact]
    public async Task Update_RenamesAndRedescribesARoleOfTheCompany()
    {
        var (client, companyId) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var own = await AddRoleAsync("Ours", companyId, "Old text");

        var response = await client.PatchAsJsonAsync($"{Prefix}/{own}", new { Name = " Ours renamed ", Description = "New text" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var stored = await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().SingleAsync(r => r.Id == own));
        stored.Name.ShouldBe("Ours renamed");
        stored.Description.ShouldBe("New text");
    }

    [Fact]
    public async Task Update_LeavesAFieldThatIsNotSent_AndAnEmptyDescriptionClearsIt()
    {
        var (client, companyId) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var own = await AddRoleAsync("Ours", companyId, "Keep me");

        (await client.PatchAsJsonAsync($"{Prefix}/{own}", new { Name = "Renamed" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().SingleAsync(r => r.Id == own))).Description.ShouldBe("Keep me");

        (await client.PatchAsJsonAsync($"{Prefix}/{own}", new { Description = "" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var stored = await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().SingleAsync(r => r.Id == own));
        stored.Name.ShouldBe("Renamed");
        stored.Description.ShouldBeNull();
    }

    [Fact]
    public async Task Update_OfASystemRole_Is409()
    {
        var (client, _) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var system = await AddRoleAsync("Dispatcher");

        (await client.PatchAsJsonAsync($"{Prefix}/{system}", new { Name = "Renamed" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().SingleAsync(r => r.Id == system))).Name.ShouldBe("Dispatcher");
    }

    [Fact]
    public async Task Update_WithANameThatIsTaken_Is409_AndARoleMayKeepItsOwnName()
    {
        var (client, companyId) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        await AddRoleAsync("Dispatcher");
        var own = await AddRoleAsync("Ours", companyId);

        (await client.PatchAsJsonAsync($"{Prefix}/{own}", new { Name = "dispatcher" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.PatchAsJsonAsync($"{Prefix}/{own}", new { Name = "OURS" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_WithABlankName_Is400_AndOfAnUnknownRole_Is404()
    {
        var (client, companyId) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var own = await AddRoleAsync("Ours", companyId);

        (await client.PatchAsJsonAsync($"{Prefix}/{own}", new { Name = "  " })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PatchAsJsonAsync($"{Prefix}/999999", new { Name = "X" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_RemovesARoleOfTheCompanyWithItsAccessRights_AndNoOthers()
    {
        var (client, companyId) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var item = await AddMenuItemAsync("Jobs");
        var own = await AddRoleAsync("Ours", companyId);
        var other = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(own, item);
        await AddGrantAsync(other, item);

        var response = await client.DeleteAsync($"{Prefix}/{own}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().AnyAsync(r => r.Id == own))).ShouldBeFalse();
        (await GrantsOfAsync(own)).ShouldBeEmpty();
        (await GrantsOfAsync(other)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Delete_OfARoleThatUsersStillHave_Is409_AndChangesNothing()
    {
        var (client, companyId) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var own = await AddRoleAsync("Ours", companyId);
        await AddMemberAsync(companyId, own, "Some One");

        (await client.DeleteAsync($"{Prefix}/{own}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().AnyAsync(r => r.Id == own))).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_OfASystemRole_Is409_AndOfAnUnknownRole_Is404()
    {
        var (client, _) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var system = await AddRoleAsync("Dispatcher");

        (await client.DeleteAsync($"{Prefix}/{system}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.DeleteAsync($"{Prefix}/999999")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().AnyAsync(r => r.Id == system))).ShouldBeTrue();
    }

    // ---- users ----

    [Fact]
    public async Task GetUsers_ListsTheCompanysUsersWithTheirRoles_MarksTheCaller_AndLeavesOutDeletedOnes()
    {
        var (client, companyId) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");
        var anna = await AddMemberAsync(companyId, dispatcher, "Anna Dispatcher");
        await AddMemberAsync(companyId, dispatcher, "Gone Person", User2CompanyStatus.Deleted);

        var users = await client.GetFromJsonAsync<List<UserRow>>($"{Prefix}/users");

        users!.Select(u => u.DisplayName).ShouldBe(["Anna Dispatcher", "Test User"]);
        var row = users.Single(u => u.UserId == anna);
        row.RoleId.ShouldBe(dispatcher);
        row.IsCurrentUser.ShouldBeFalse();
        row.Email.ShouldBe("anna.dispatcher@example.test");
        users.Single(u => u.IsCurrentUser).UserId.ShouldBe(_fixture.TestUserId);
    }

    [Fact]
    public async Task GetUsers_LeavesOutTheUsersOfOtherCompanies()
    {
        var (client, _) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var otherCompany = await _fixture.QueryDbAsync(async db =>
        {
            var company = new Company { Name = "Other", StatusId = 1 };
            db.Set<Company>().Add(company);
            await db.SaveChangesAsync();
            return company.Id;
        });
        var role = await AddRoleAsync("Dispatcher");
        await AddMemberAsync(otherCompany, role, "Far Away");

        (await client.GetFromJsonAsync<List<UserRow>>($"{Prefix}/users"))!.Select(u => u.DisplayName).ShouldNotContain("Far Away");
    }

    // ---- giving users a role ----

    [Fact]
    public async Task Assign_GivesTheUsersTheRole_ReplacingTheirOldOne_AndLeavesOthersAlone()
    {
        var (client, companyId) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");
        var billing = await AddRoleAsync("Billing");
        var anna = await AddMemberAsync(companyId, dispatcher, "Anna One");
        var bob = await AddMemberAsync(companyId, dispatcher, "Bob Two");
        var cy = await AddMemberAsync(companyId, dispatcher, "Cy Three");

        var response = await client.PostAsJsonAsync($"{Prefix}/{billing}/members", new { UserIds = new[] { anna, bob } });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RoleOfAsync(companyId, anna)).ShouldBe(billing);
        (await RoleOfAsync(companyId, bob)).ShouldBe(billing);
        (await RoleOfAsync(companyId, cy)).ShouldBe(dispatcher);
    }

    [Fact]
    public async Task Assign_ToTheCallersOwnRole_Is409_AndChangesNothing()
    {
        var (client, companyId) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var billing = await AddRoleAsync("Billing");
        var dispatcher = await AddRoleAsync("Dispatcher");
        var anna = await AddMemberAsync(companyId, dispatcher, "Anna One");

        var response = await client.PostAsJsonAsync($"{Prefix}/{billing}/members", new { UserIds = new[] { anna, _fixture.TestUserId } });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await RoleOfAsync(companyId, anna)).ShouldBe(dispatcher);
    }

    [Fact]
    public async Task Assign_WithAUserWhoIsNotAMemberOrIsDeleted_Is400_AndChangesNothing()
    {
        var (client, companyId) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var billing = await AddRoleAsync("Billing");
        var dispatcher = await AddRoleAsync("Dispatcher");
        var anna = await AddMemberAsync(companyId, dispatcher, "Anna One");
        var gone = await AddMemberAsync(companyId, dispatcher, "Gone Person", User2CompanyStatus.Deleted);

        (await client.PostAsJsonAsync($"{Prefix}/{billing}/members", new { UserIds = new[] { anna, 999999 } })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync($"{Prefix}/{billing}/members", new { UserIds = new[] { anna, gone } })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await RoleOfAsync(companyId, anna)).ShouldBe(dispatcher);
    }

    [Fact]
    public async Task Assign_WithAUserTheSiteAdminSuspended_Is409()
    {
        var (client, companyId) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var billing = await AddRoleAsync("Billing");
        var dispatcher = await AddRoleAsync("Dispatcher");
        var locked = await AddMemberAsync(companyId, dispatcher, "Locked Out", User2CompanyStatus.SuspendedBySiteAdmin);

        (await client.PostAsJsonAsync($"{Prefix}/{billing}/members", new { UserIds = new[] { locked } })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await RoleOfAsync(companyId, locked)).ShouldBe(dispatcher);
    }

    [Fact]
    public async Task Assign_ToAnUnknownRole_Is404_AndNoUsersIsFine()
    {
        var (client, _) = await PortalAdminTestClients.CreatePortalAdminAsync(_fixture);
        var billing = await AddRoleAsync("Billing");

        (await client.PostAsJsonAsync($"{Prefix}/999999/members", new { UserIds = new[] { 1 } })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync($"{Prefix}/{billing}/members", new { UserIds = Array.Empty<int>() })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
