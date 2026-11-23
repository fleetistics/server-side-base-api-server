using System.Net;
using System.Net.Http.Json;
using db_model.AppStructure;
using db_model.Companies;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// /api/admin/roles (RolesAdminController): the default access rights of the system roles, i.e. the Role2MenuItem rows with CompanyId null.
/// Any signed-in user may call it for now (there is no role check yet). The schema comes from the model (EnsureCreated), so the tests
/// seed the menu items and roles they need.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DefaultRolesControllerTests : IAsyncLifetime
{
    private const string Prefix = "/api/admin/roles";

    private readonly ApiFixture _fixture;

    public DefaultRolesControllerTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed record RoleRow(int Id, string Name, bool IsSystem, bool IsOverridden, int MemberCount, int TotalMenus, int ViewCount, int CreateCount, int EditCount, int DeleteCount);
    private sealed record ItemRow(int Id, string Title, bool CanView, bool CanCreate, bool CanEdit, bool CanDelete, List<ItemRow> Children);
    private sealed record PermissionsRow(int RoleId, string RoleName, bool IsSystem, bool IsOverridden, List<ItemRow> Items);
    private sealed record MenuRow(int Id, string Title, bool CanEdit, List<MenuRow> Children);

    private sealed record Seed(int Group, int ChildA, int ChildB);

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

    private Task<int> AddCompanyAsync(string name) => _fixture.QueryDbAsync(async db =>
    {
        var company = new Company { Name = name, StatusId = 1 };
        db.Set<Company>().Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    });

    private Task AddGrantAsync(int roleId, int itemId, bool view = true, bool create = false, bool edit = false, bool delete = false, int? companyId = null) => _fixture.QueryDbAsync(async db =>
    {
        db.Set<Role2MenuItem>().Add(new Role2MenuItem { PermissionRoleId = roleId, CompanyId = companyId, ApplicationMenuItemId = itemId, CanView = view, CanCreate = create, CanEdit = edit, CanDelete = delete });
        await db.SaveChangesAsync();
        return 0;
    });

    private Task<List<Role2MenuItem>> RowsOfAsync(int roleId) =>
        _fixture.QueryDbAsync(db => db.Set<Role2MenuItem>().Where(r => r.PermissionRoleId == roleId).AsNoTracking().OrderBy(r => r.ApplicationMenuItemId).ToListAsync());

    private Task<int> RoleIdAsync(string name) =>
        _fixture.QueryDbAsync(db => db.Set<PermissionRole>().Where(r => r.Name == name && r.CompanyId == null).Select(r => r.Id).SingleAsync());

    private static object Put(params (int Item, bool View, bool Create, bool Edit, bool Delete)[] grants) =>
        new { Items = grants.Select(g => new { ApplicationMenuItemId = g.Item, CanView = g.View, CanCreate = g.Create, CanEdit = g.Edit, CanDelete = g.Delete }).ToArray() };

    private static IEnumerable<MenuRow> Flatten(IEnumerable<MenuRow> items) => items.SelectMany(i => new[] { i }.Concat(Flatten(i.Children)));

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
    public async Task EveryEndpoint_ForASignedInUserWithNoCompany_IsLetIn()
    {
        var seed = await SeedMenuAsync();
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var dispatcher = await AddRoleAsync("Dispatcher");

        (await client.GetAsync(Prefix)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync($"{Prefix}/{dispatcher}/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildA, true, false, false, false)))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // ---- reading ----

    [Fact]
    public async Task GetRoles_ListsTheSystemRoles_WithMembersAndHowMuchOfTheMenuEachMayUseByDefault()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(dispatcher, seed.ChildA, edit: true);
        await AddGrantAsync(dispatcher, seed.ChildB);

        var roles = await client.GetFromJsonAsync<List<RoleRow>>(Prefix);

        var row = roles!.Single(r => r.Name == "Dispatcher");
        row.IsSystem.ShouldBeTrue();
        row.IsOverridden.ShouldBeFalse();
        row.MemberCount.ShouldBe(0);
        row.TotalMenus.ShouldBe(3);
        row.ViewCount.ShouldBe(2);
        row.EditCount.ShouldBe(1);
        row.CreateCount.ShouldBe(0);
        row.DeleteCount.ShouldBe(0);
        roles.Single(r => r.Name == "Administrator").MemberCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetRoles_LeavesOutTheRolesOfCompanies_AndIgnoresTheirOverrides()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var other = await AddCompanyAsync("Other");
        var dispatcher = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(dispatcher, seed.ChildA);
        await AddGrantAsync(dispatcher, seed.ChildA, view: false, companyId: companyId);
        await AddRoleAsync("Ours", companyId);
        await AddRoleAsync("Theirs", other);

        var roles = await client.GetFromJsonAsync<List<RoleRow>>(Prefix);

        roles!.Select(r => r.Name).ShouldNotContain("Ours");
        roles.Select(r => r.Name).ShouldNotContain("Theirs");
        var row = roles.Single(r => r.Name == "Dispatcher");
        row.ViewCount.ShouldBe(1);
        row.IsOverridden.ShouldBeFalse();
    }

    [Fact]
    public async Task GetMenuPermissions_GivesTheWholeMenuAsATree_WithTheDefaultFlags()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(dispatcher, seed.ChildA, create: true, edit: true);

        var result = await client.GetFromJsonAsync<PermissionsRow>($"{Prefix}/{dispatcher}/menu-permissions");

        result!.RoleName.ShouldBe("Dispatcher");
        result.IsSystem.ShouldBeTrue();
        result.IsOverridden.ShouldBeFalse();
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
    public async Task GetMenuPermissions_ShowsTheDefaults_EvenWhenTheCallersCompanyOverridesTheRole()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var administrator = await RoleIdAsync("Administrator");
        await AddGrantAsync(administrator, seed.ChildA);
        await AddGrantAsync(administrator, seed.ChildB, companyId: companyId);

        var result = await client.GetFromJsonAsync<PermissionsRow>($"{Prefix}/{administrator}/menu-permissions");

        result!.IsOverridden.ShouldBeFalse();
        var children = result.Items.Single().Children;
        children.Single(c => c.Title == "Calendar").CanView.ShouldBeTrue();
        children.Single(c => c.Title == "Tasks").CanView.ShouldBeFalse();
    }

    [Fact]
    public async Task GetMenuPermissions_ForAnUnknownRole_Is404()
    {
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);

        (await client.GetAsync($"{Prefix}/999999/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMenuPermissions_ForTheRoleOfACompany_Is404()
    {
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var other = await AddCompanyAsync("Other");
        var ours = await AddRoleAsync("Ours", companyId);
        var theirs = await AddRoleAsync("Theirs", other);

        (await client.GetAsync($"{Prefix}/{ours}/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"{Prefix}/{theirs}/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- writing ----

    [Fact]
    public async Task Put_StoresOnlyTheGrantedRows_WithNoCompany_AndAnyTickedFlagImpliesView()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");

        var response = await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put(
            (seed.Group, false, false, false, false),
            (seed.ChildA, true, false, false, false),
            (seed.ChildB, false, true, true, true)));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var rows = await RowsOfAsync(dispatcher);
        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(r => r.CompanyId == null);
        var viewOnly = rows.Single(r => r.ApplicationMenuItemId == seed.ChildA);
        (viewOnly.CanView, viewOnly.CanCreate, viewOnly.CanEdit, viewOnly.CanDelete).ShouldBe((true, false, false, false));
        var all = rows.Single(r => r.ApplicationMenuItemId == seed.ChildB);
        (all.CanView, all.CanCreate, all.CanEdit, all.CanDelete).ShouldBe((true, true, true, true));
    }

    [Fact]
    public async Task Put_ReplacesTheDefaultsTheRoleHad_ItemsLeftOutAreTakenAway()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
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
    public async Task Put_WithNothingTicked_TakesEveryDefaultAway_AndOtherRolesAndOverridesAreUntouched()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");
        var billing = await AddRoleAsync("Billing");
        await AddGrantAsync(dispatcher, seed.ChildA);
        await AddGrantAsync(dispatcher, seed.ChildA, companyId: companyId);
        await AddGrantAsync(billing, seed.ChildA);

        var response = await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildA, false, false, false, false)));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var rows = await RowsOfAsync(dispatcher);
        rows.Single().CompanyId.ShouldBe(companyId);
        (await RowsOfAsync(billing)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Put_ThenTheDefaultTree_FollowsTheNewFlags()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
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
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
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
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");

        var response = await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildA, true, false, false, false), (seed.ChildA, true, true, false, false)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RowsOfAsync(dispatcher)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_ForAnUnknownRole_Is404()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);

        (await client.PutAsJsonAsync($"{Prefix}/999999/menu-permissions", Put((seed.ChildA, true, false, false, false)))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_ForTheRoleOfACompany_Is404_AndChangesNothing()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var ours = await AddRoleAsync("Ours", companyId);

        (await client.PutAsJsonAsync($"{Prefix}/{ours}/menu-permissions", Put((seed.ChildA, true, false, false, false)))).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await RowsOfAsync(ours)).ShouldBeEmpty();
    }

    // ---- what it does to the menu of a company ----

    [Fact]
    public async Task ADefaultsChange_ShowsInTheMenuOfAUserWhoseCompanyHasNoOverride()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var administrator = await RoleIdAsync("Administrator");
        (await client.GetFromJsonAsync<List<MenuRow>>("/api/menu"))!.ShouldBeEmpty();

        (await client.PutAsJsonAsync($"{Prefix}/{administrator}/menu-permissions", Put((seed.ChildA, true, false, false, false)))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var menu = await client.GetFromJsonAsync<List<MenuRow>>("/api/menu");
        Flatten(menu!).Select(i => i.Title).ShouldBe(["Jobs", "Calendar"]);

        (await client.PutAsJsonAsync($"{Prefix}/{administrator}/menu-permissions", Put((seed.ChildB, true, false, true, false)))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        menu = await client.GetFromJsonAsync<List<MenuRow>>("/api/menu");
        Flatten(menu!).Select(i => i.Title).ShouldBe(["Jobs", "Tasks"]);
        Flatten(menu!).Single(i => i.Title == "Tasks").CanEdit.ShouldBeTrue();
    }

    [Fact]
    public async Task ADefaultsChange_DoesNotReachAUserWhoseCompanyOverridesTheRole()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var administrator = await RoleIdAsync("Administrator");
        await AddGrantAsync(administrator, seed.ChildA, companyId: companyId);
        await AddGrantAsync(administrator, seed.ChildB, view: false, companyId: companyId);

        await client.PutAsJsonAsync($"{Prefix}/{administrator}/menu-permissions", Put((seed.ChildB, true, false, false, false)));

        var menu = await client.GetFromJsonAsync<List<MenuRow>>("/api/menu");
        Flatten(menu!).Select(i => i.Title).ShouldBe(["Jobs", "Calendar"]);
    }
}
