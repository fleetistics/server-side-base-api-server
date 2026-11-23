using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using api_server.core;
using db_model.AppStructure;
using db_model.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// /api/admin/roles (RoleMenuPermissionsController): which menu items each role may use. Portal Admin only. The schema comes from the
/// model (EnsureCreated), so the tests seed the menu items and roles they need.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RoleMenuPermissionsControllerTests : IAsyncLifetime
{
    private const string Prefix = "/api/admin/roles";

    private readonly ApiFixture _fixture;

    public RoleMenuPermissionsControllerTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed record RoleRow(int Id, string Name, bool IsSystem, bool IsReadOnly, int MemberCount, int TotalMenus, int ViewCount, int CreateCount, int EditCount, int DeleteCount);
    private sealed record ItemRow(int Id, string Title, bool CanView, bool CanCreate, bool CanEdit, bool CanDelete, List<ItemRow> Children);
    private sealed record PermissionsRow(int RoleId, string RoleName, bool IsReadOnly, List<ItemRow> Items);

    private sealed record Seed(int Group, int ChildA, int ChildB);

    /// <summary>A client signed in as the seeded user, working in a new company where they hold the system-wide role with this name.</summary>
    private async Task<(HttpClient Client, int CompanyId)> CreateClientAsync(string roleName)
    {
        var client = _fixture.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<ApiFixture.LoginResponse>();

        var companyId = await _fixture.QueryDbAsync(async db =>
        {
            var company = new Company { Name = "Test Company", StatusId = 1 };
            var role = await db.Set<PermissionRole>().FirstOrDefaultAsync(r => r.Name == roleName && r.CompanyId == null)
                ?? db.Set<PermissionRole>().Add(new PermissionRole { Name = roleName, CompanyId = null }).Entity;
            db.Set<Company>().Add(company);
            await db.SaveChangesAsync();
            db.Set<User2Company>().Add(new User2Company
            {
                CompanyId = company.Id,
                UserId = _fixture.TestUserId,
                PermissionRoleId = role.Id,
                StatusId = 1,
                JoinTypeId = 1,
                JoinedDate = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return company.Id;
        });

        var tokens = _fixture.Services.GetRequiredService<api_server.Auth.TokenService>();
        var token = tokens.CreateAccessToken(session!.UserId.ToString(), session.SessionId.ToString(), companyId: companyId.ToString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, companyId);
    }

    private Task<(HttpClient Client, int CompanyId)> CreatePortalAdminClientAsync() => CreateClientAsync(AuthPolicies.PortalAdminRole);

    /// <summary>A group with two children in menu 1.</summary>
    private Task<Seed> SeedMenuAsync() => _fixture.QueryDbAsync(async db =>
    {
        var group = new ApplicationMenuItem { MenuId = 1, Title = "Jobs", OrderBy = 1, Path = "/jobs" };
        db.Set<ApplicationMenuItem>().Add(group);
        await db.SaveChangesAsync();
        var a = new ApplicationMenuItem { MenuId = 1, Title = "Calendar", ParentId = group.Id, OrderBy = 1, Path = "/jobs/schedule" };
        var b = new ApplicationMenuItem { MenuId = 1, Title = "Tasks", ParentId = group.Id, OrderBy = 2, Path = "/jobs/tasks" };
        db.Set<ApplicationMenuItem>().AddRange(a, b);
        await db.SaveChangesAsync();
        return new Seed(group.Id, a.Id, b.Id);
    });

    private Task<int> AddRoleAsync(string name, int? companyId = null) => _fixture.QueryDbAsync(async db =>
    {
        var role = new PermissionRole { Name = name, CompanyId = companyId };
        db.Set<PermissionRole>().Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    });

    private Task AddGrantAsync(int roleId, int itemId, bool view = true, bool create = false, bool edit = false, bool delete = false) => _fixture.QueryDbAsync(async db =>
    {
        db.Set<Role2MenuItem>().Add(new Role2MenuItem { PermissionRoleId = roleId, ApplicationMenuItemId = itemId, CanView = view, CanCreate = create, CanEdit = edit, CanDelete = delete });
        await db.SaveChangesAsync();
        return 0;
    });

    private Task<List<Role2MenuItem>> RowsOfAsync(int roleId) =>
        _fixture.QueryDbAsync(db => db.Set<Role2MenuItem>().Where(r => r.PermissionRoleId == roleId).AsNoTracking().OrderBy(r => r.ApplicationMenuItemId).ToListAsync());

    private static object Put(params (int Item, bool View, bool Create, bool Edit, bool Delete)[] grants) =>
        new { Items = grants.Select(g => new { ApplicationMenuItemId = g.Item, CanView = g.View, CanCreate = g.Create, CanEdit = g.Edit, CanDelete = g.Delete }).ToArray() };

    // ---- who may call ----

    [Fact]
    public async Task EveryEndpoint_WithoutALogin_Is401()
    {
        var anonymous = _fixture.CreateClient();

        (await anonymous.GetAsync(Prefix)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"{Prefix}/1/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PutAsJsonAsync($"{Prefix}/1/menu-permissions", Put())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EveryEndpoint_ForAnAdministratorWhoIsNotAPortalAdmin_Is403_AndWritesNothing()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CreateClientAsync("Administrator");
        var roleId = await AddRoleAsync("Dispatcher");

        (await client.GetAsync(Prefix)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync($"{Prefix}/{roleId}/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync($"{Prefix}/{roleId}/menu-permissions", Put((seed.ChildA, true, false, false, false)))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await RowsOfAsync(roleId)).ShouldBeEmpty();
    }

    // ---- reading ----

    [Fact]
    public async Task GetRoles_ListsTheRoles_WithMembersAndHowMuchOfTheMenuEachMayUse()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CreatePortalAdminClientAsync();
        var dispatcher = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(dispatcher, seed.ChildA, edit: true);
        await AddGrantAsync(dispatcher, seed.ChildB);

        var roles = await client.GetFromJsonAsync<List<RoleRow>>(Prefix);

        var row = roles!.Single(r => r.Name == "Dispatcher");
        row.IsSystem.ShouldBeTrue();
        row.IsReadOnly.ShouldBeFalse();
        row.MemberCount.ShouldBe(0);
        row.TotalMenus.ShouldBe(3);
        row.ViewCount.ShouldBe(2);
        row.EditCount.ShouldBe(1);
        row.CreateCount.ShouldBe(0);
        row.DeleteCount.ShouldBe(0);
        var portalAdmin = roles.Single(r => r.Name == AuthPolicies.PortalAdminRole);
        portalAdmin.IsReadOnly.ShouldBeTrue();
        portalAdmin.MemberCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetRoles_LeavesOutTheRolesOfOtherCompanies()
    {
        var (client, companyId) = await CreatePortalAdminClientAsync();
        var other = await _fixture.QueryDbAsync(async db =>
        {
            var company = new Company { Name = "Other", StatusId = 1 };
            db.Set<Company>().Add(company);
            await db.SaveChangesAsync();
            return company.Id;
        });
        await AddRoleAsync("Ours", companyId);
        await AddRoleAsync("Theirs", other);

        var names = (await client.GetFromJsonAsync<List<RoleRow>>(Prefix))!.Select(r => r.Name).ToList();

        names.ShouldContain("Ours");
        names.ShouldNotContain("Theirs");
    }

    [Fact]
    public async Task GetMenuPermissions_GivesTheWholeMenuAsATree_WithTheRolesFlags()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CreatePortalAdminClientAsync();
        var dispatcher = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(dispatcher, seed.ChildA, create: true, edit: true);

        var result = await client.GetFromJsonAsync<PermissionsRow>($"{Prefix}/{dispatcher}/menu-permissions");

        result!.RoleName.ShouldBe("Dispatcher");
        result.IsReadOnly.ShouldBeFalse();
        var jobs = result.Items.Single();
        jobs.Title.ShouldBe("Jobs");
        jobs.CanView.ShouldBeFalse();
        jobs.Children.Select(c => c.Title).ShouldBe(["Calendar", "Tasks"]);
        var calendar = jobs.Children[0];
        (calendar.CanView, calendar.CanCreate, calendar.CanEdit, calendar.CanDelete).ShouldBe((true, true, true, false));
        var tasks = jobs.Children[1];
        (tasks.CanView, tasks.CanCreate, tasks.CanEdit, tasks.CanDelete).ShouldBe((false, false, false, false));
    }

    [Fact]
    public async Task GetMenuPermissions_ForAnUnknownRole_Is404()
    {
        var (client, _) = await CreatePortalAdminClientAsync();

        (await client.GetAsync($"{Prefix}/999999/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMenuPermissions_ForTheRoleOfAnotherCompany_Is404()
    {
        var (client, _) = await CreatePortalAdminClientAsync();
        var other = await _fixture.QueryDbAsync(async db =>
        {
            var company = new Company { Name = "Other", StatusId = 1 };
            db.Set<Company>().Add(company);
            await db.SaveChangesAsync();
            return company.Id;
        });
        var theirs = await AddRoleAsync("Theirs", other);

        (await client.GetAsync($"{Prefix}/{theirs}/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- writing ----

    [Fact]
    public async Task Put_StoresTheFlags_AndAnyTickedFlagImpliesView()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CreatePortalAdminClientAsync();
        var dispatcher = await AddRoleAsync("Dispatcher");

        var response = await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put(
            (seed.ChildA, true, false, false, false),
            (seed.ChildB, false, true, true, true)));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var rows = await RowsOfAsync(dispatcher);
        rows.Count.ShouldBe(2);
        var viewOnly = rows.Single(r => r.ApplicationMenuItemId == seed.ChildA);
        (viewOnly.CanView, viewOnly.CanCreate, viewOnly.CanEdit, viewOnly.CanDelete).ShouldBe((true, false, false, false));
        var all = rows.Single(r => r.ApplicationMenuItemId == seed.ChildB);
        (all.CanView, all.CanCreate, all.CanEdit, all.CanDelete).ShouldBe((true, true, true, true));
    }

    [Fact]
    public async Task Put_ReplacesWhatTheRoleHad_ItemsLeftOutAreTakenAway()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CreatePortalAdminClientAsync();
        var dispatcher = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(dispatcher, seed.Group);
        await AddGrantAsync(dispatcher, seed.ChildA, edit: true);

        var response = await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put(
            (seed.ChildA, true, false, false, false),
            (seed.ChildB, true, false, false, false)));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var rows = await RowsOfAsync(dispatcher);
        rows.Select(r => r.ApplicationMenuItemId).ShouldBe([seed.ChildA, seed.ChildB]);
        rows.Single(r => r.ApplicationMenuItemId == seed.ChildA).CanEdit.ShouldBe(false);
    }

    [Fact]
    public async Task Put_WithNothingTicked_TakesEverythingAway_AndOtherRolesAreUntouched()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CreatePortalAdminClientAsync();
        var dispatcher = await AddRoleAsync("Dispatcher");
        var billing = await AddRoleAsync("Billing");
        await AddGrantAsync(dispatcher, seed.ChildA);
        await AddGrantAsync(billing, seed.ChildA);

        var response = await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildA, false, false, false, false)));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RowsOfAsync(dispatcher)).ShouldBeEmpty();
        (await RowsOfAsync(billing)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Put_ThenTheRolesMenu_FollowsTheNewFlags()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CreatePortalAdminClientAsync();
        var dispatcher = await AddRoleAsync("Dispatcher");

        await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildB, false, false, true, false)));

        var result = await client.GetFromJsonAsync<PermissionsRow>($"{Prefix}/{dispatcher}/menu-permissions");
        var tasks = result!.Items.Single().Children.Single(c => c.Title == "Tasks");
        (tasks.CanView, tasks.CanEdit).ShouldBe((true, true));
    }

    [Fact]
    public async Task Put_WithAnItemThatIsNotInTheMenu_Is400_AndChangesNothing()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CreatePortalAdminClientAsync();
        var dispatcher = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(dispatcher, seed.ChildA);

        var response = await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildB, true, false, false, false), (999999, true, false, false, false)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RowsOfAsync(dispatcher)).Select(r => r.ApplicationMenuItemId).ShouldBe([seed.ChildA]);
    }

    [Fact]
    public async Task Put_WithTheSameItemTwice_Is400_AndChangesNothing()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CreatePortalAdminClientAsync();
        var dispatcher = await AddRoleAsync("Dispatcher");

        var response = await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildA, true, false, false, false), (seed.ChildA, true, true, false, false)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RowsOfAsync(dispatcher)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_ForPortalAdmin_Is409_AndChangesNothing()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CreatePortalAdminClientAsync();
        var portalAdminId = await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().Where(r => r.Name == AuthPolicies.PortalAdminRole).Select(r => r.Id).SingleAsync());

        var response = await client.PutAsJsonAsync($"{Prefix}/{portalAdminId}/menu-permissions", Put((seed.ChildA, true, false, false, false)));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await RowsOfAsync(portalAdminId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_ForAnUnknownRole_Is404()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CreatePortalAdminClientAsync();

        (await client.PutAsJsonAsync($"{Prefix}/999999/menu-permissions", Put((seed.ChildA, true, false, false, false)))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
