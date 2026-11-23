using System.Net;
using System.Net.Http.Json;
using db_model.AppStructure;
using db_model.Companies;
using db_model.UserManagement;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// /api/company/roles (CompanyRolesController): the roles of the caller's company, their access rights (a system role's rights are
/// changed for the company only, as an override), and who has which role. Any signed-in member of a company may call it for now
/// (there is no role check yet). The schema comes from the model (EnsureCreated), so the tests seed what they need.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CompanyRolesControllerTests : IAsyncLifetime
{
    private const string Prefix = "/api/company/roles";

    private readonly ApiFixture _fixture;

    public CompanyRolesControllerTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed record RoleRow(int Id, string Name, string? Description, bool IsSystem, bool IsOverridden, int MemberCount, int TotalMenus, int ViewCount, int CreateCount, int EditCount, int DeleteCount);
    private sealed record ItemRow(int Id, string Title, bool CanView, bool CanCreate, bool CanEdit, bool CanDelete, List<ItemRow> Children);
    private sealed record PermissionsRow(int RoleId, string RoleName, bool IsSystem, bool IsOverridden, List<ItemRow> Items);
    private sealed record UserRow(int UserId, string DisplayName, string? Email, short StatusId, int RoleId, bool IsCurrentUser);
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

    private Task<int> AddRoleAsync(string name, int? companyId = null, string? description = null) => _fixture.QueryDbAsync(async db =>
    {
        var role = new PermissionRole { Name = name, CompanyId = companyId, Description = description };
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

    private Task<int> AddMenuItemAsync(string title) => _fixture.QueryDbAsync(async db =>
    {
        var item = new ApplicationMenuItem { MenuId = 1, Title = title, Path = "/" + title.ToLowerInvariant() };
        db.Set<ApplicationMenuItem>().Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    });

    /// <summary>One right of a role: a default (companyId null) or a row of the company.</summary>
    private Task AddGrantAsync(int roleId, int itemId, bool view = true, bool create = false, bool edit = false, bool delete = false, int? companyId = null) => _fixture.QueryDbAsync(async db =>
    {
        db.Set<Role2MenuItem>().Add(new Role2MenuItem { PermissionRoleId = roleId, CompanyId = companyId, ApplicationMenuItemId = itemId, CanView = view, CanCreate = create, CanEdit = edit, CanDelete = delete });
        await db.SaveChangesAsync();
        return 0;
    });

    private Task<List<Role2MenuItem>> RowsOfAsync(int roleId) =>
        _fixture.QueryDbAsync(db => db.Set<Role2MenuItem>().Where(g => g.PermissionRoleId == roleId).AsNoTracking().OrderBy(g => g.CompanyId).ThenBy(g => g.ApplicationMenuItemId).ToListAsync());

    private async Task<List<Role2MenuItem>> DefaultRowsOfAsync(int roleId) => (await RowsOfAsync(roleId)).Where(g => g.CompanyId == null).ToList();

    private async Task<List<Role2MenuItem>> CompanyRowsOfAsync(int roleId, int companyId) => (await RowsOfAsync(roleId)).Where(g => g.CompanyId == companyId).ToList();

    private Task<int> RoleIdAsync(string name) =>
        _fixture.QueryDbAsync(db => db.Set<PermissionRole>().Where(r => r.Name == name && r.CompanyId == null).Select(r => r.Id).SingleAsync());

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

    private static object Put(params (int Item, bool View, bool Create, bool Edit, bool Delete)[] grants) =>
        new { Items = grants.Select(g => new { ApplicationMenuItemId = g.Item, CanView = g.View, CanCreate = g.Create, CanEdit = g.Edit, CanDelete = g.Delete }).ToArray() };

    private static IEnumerable<MenuRow> Flatten(IEnumerable<MenuRow> items) => items.SelectMany(i => new[] { i }.Concat(Flatten(i.Children)));

    private static async Task<IReadOnlyList<string>> MenuTitlesAsync(HttpClient client) =>
        Flatten((await client.GetFromJsonAsync<List<MenuRow>>("/api/menu"))!).Select(i => i.Title).ToList();

    private static async Task<RoleRow> RoleSummaryAsync(HttpClient client, int roleId) =>
        (await client.GetFromJsonAsync<List<RoleRow>>(Prefix))!.Single(r => r.Id == roleId);

    // ---- who may call ----

    [Fact]
    public async Task EveryEndpoint_WithoutALogin_Is401()
    {
        var anonymous = _fixture.CreateClient();

        (await anonymous.GetAsync(Prefix)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync(Prefix, new { Name = "New" })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PatchAsJsonAsync($"{Prefix}/1", new { Name = "Renamed" })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync($"{Prefix}/1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"{Prefix}/1/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PutAsJsonAsync($"{Prefix}/1/menu-permissions", Put())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync($"{Prefix}/1/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"{Prefix}/users")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync($"{Prefix}/1/members", new { UserIds = new[] { 1 } })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EveryEndpoint_ForASignedInUserWithNoCompany_Is403_AndChangesNothing()
    {
        var seed = await SeedMenuAsync();
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var dispatcher = await AddRoleAsync("Dispatcher");

        (await client.GetAsync(Prefix)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync(Prefix, new { Name = "New" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PatchAsJsonAsync($"{Prefix}/{dispatcher}", new { Name = "Renamed" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.DeleteAsync($"{Prefix}/{dispatcher}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync($"{Prefix}/{dispatcher}/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildA, true, false, false, false)))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.DeleteAsync($"{Prefix}/{dispatcher}/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync($"{Prefix}/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync($"{Prefix}/{dispatcher}/members", new { UserIds = new[] { 1 } })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().Select(r => r.Name).ToListAsync())).ShouldBe(["Dispatcher"]);
        (await RowsOfAsync(dispatcher)).ShouldBeEmpty();
    }

    // ---- the roles ----

    [Fact]
    public async Task GetRoles_ListsTheSystemRolesAndTheCompanysOwn_WithMembersAndHowMuchOfTheMenuEachMayUse()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");
        var ours = await AddRoleAsync("Ours", companyId);
        await AddGrantAsync(dispatcher, seed.ChildA, edit: true);
        await AddGrantAsync(dispatcher, seed.ChildB);
        await AddGrantAsync(ours, seed.ChildA, companyId: companyId);

        var roles = await client.GetFromJsonAsync<List<RoleRow>>(Prefix);

        var system = roles!.Single(r => r.Name == "Dispatcher");
        system.IsSystem.ShouldBeTrue();
        system.IsOverridden.ShouldBeFalse();
        system.MemberCount.ShouldBe(0);
        system.TotalMenus.ShouldBe(3);
        system.ViewCount.ShouldBe(2);
        system.EditCount.ShouldBe(1);
        system.CreateCount.ShouldBe(0);
        system.DeleteCount.ShouldBe(0);
        var own = roles.Single(r => r.Name == "Ours");
        own.IsSystem.ShouldBeFalse();
        own.ViewCount.ShouldBe(1);
        roles.Single(r => r.Name == "Administrator").MemberCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetRoles_LeavesOutTheRolesOfOtherCompanies_AndCountsOnlyTheCallersMembers()
    {
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var other = await AddCompanyAsync("Other");
        var dispatcher = await AddRoleAsync("Dispatcher");
        await AddRoleAsync("Ours", companyId);
        await AddRoleAsync("Theirs", other);
        await AddMemberAsync(other, dispatcher, "Far Away");

        var roles = await client.GetFromJsonAsync<List<RoleRow>>(Prefix);

        roles!.Select(r => r.Name).ShouldContain("Ours");
        roles.Select(r => r.Name).ShouldNotContain("Theirs");
        roles.Single(r => r.Name == "Dispatcher").MemberCount.ShouldBe(0);
    }

    // ---- create ----

    [Fact]
    public async Task Create_MakesARoleOfTheCallersCompany_WithATrimmedNameAndNoAccessRights()
    {
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);

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
        (await RowsOfAsync(created.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_WithASystemRoleToCopy_StartsWithItsDefaultRights_AsRowsOfTheCompany_AndLeavesTheSourceAlone()
    {
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
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
        var copied = await RowsOfAsync(created.Id);
        copied.ShouldAllBe(g => g.CompanyId == companyId);
        copied.Select(g => (g.ApplicationMenuItemId, g.CanView, g.CanEdit)).ShouldBe([(a, true, true), (b, true, false)]);
        var original = await RowsOfAsync(source);
        original.Count.ShouldBe(2);
        original.ShouldAllBe(g => g.CompanyId == null);
    }

    [Fact]
    public async Task Create_WithARoleToCopyThatTheCompanyOverrides_CopiesTheOverride_NotTheDefaults()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var source = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(source, seed.ChildA);
        await AddGrantAsync(source, seed.ChildB);
        await client.PutAsJsonAsync($"{Prefix}/{source}/menu-permissions", Put((seed.ChildB, true, false, true, false)));

        var response = await client.PostAsJsonAsync(Prefix, new { Name = "Dispatcher copy", CopyFromRoleId = source });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<RoleRow>();
        created!.ViewCount.ShouldBe(1);
        created.EditCount.ShouldBe(1);
        var copied = await RowsOfAsync(created.Id);
        copied.ShouldAllBe(g => g.CompanyId == companyId);
        copied.Where(g => g.CanView == true).Select(g => g.ApplicationMenuItemId).ShouldBe([seed.ChildB]);
        (await CompanyRowsOfAsync(source, companyId)).Count.ShouldBe(3);
        (await DefaultRowsOfAsync(source)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Create_WithAnOwnRoleToCopy_CopiesItsRights()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var ours = await AddRoleAsync("Ours", companyId);
        await AddGrantAsync(ours, seed.ChildA, edit: true, companyId: companyId);

        var response = await client.PostAsJsonAsync(Prefix, new { Name = "Ours copy", CopyFromRoleId = ours });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<RoleRow>();
        (await CompanyRowsOfAsync(created!.Id, companyId)).Select(g => (g.ApplicationMenuItemId, g.CanEdit)).ShouldBe([(seed.ChildA, (bool?)true)]);
        (await RowsOfAsync(ours)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Create_WithABlankName_Is400()
    {
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);

        (await client.PostAsJsonAsync(Prefix, new { Name = "   " })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(Prefix, new { Name = new string('x', 101) })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithANameThatIsTaken_IgnoringCase_Is409()
    {
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        await AddRoleAsync("Dispatcher");

        (await client.PostAsJsonAsync(Prefix, new { Name = "dispatcher" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithARoleToCopyThatTheCompanyCannotUse_Is400_AndCreatesNothing()
    {
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var otherCompany = await AddCompanyAsync("Other");
        var theirs = await AddRoleAsync("Theirs", otherCompany);

        (await client.PostAsJsonAsync(Prefix, new { Name = "Mine", CopyFromRoleId = theirs })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(Prefix, new { Name = "Mine", CopyFromRoleId = 999999 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().AnyAsync(r => r.Name == "Mine"))).ShouldBeFalse();
    }

    // ---- rename ----

    [Fact]
    public async Task Update_RenamesAndRedescribesARoleOfTheCompany()
    {
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
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
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
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
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var system = await AddRoleAsync("Dispatcher");

        (await client.PatchAsJsonAsync($"{Prefix}/{system}", new { Name = "Renamed" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().SingleAsync(r => r.Id == system))).Name.ShouldBe("Dispatcher");
    }

    [Fact]
    public async Task Update_WithANameThatIsTaken_Is409_AndARoleMayKeepItsOwnName()
    {
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        await AddRoleAsync("Dispatcher");
        var own = await AddRoleAsync("Ours", companyId);

        (await client.PatchAsJsonAsync($"{Prefix}/{own}", new { Name = "dispatcher" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.PatchAsJsonAsync($"{Prefix}/{own}", new { Name = "OURS" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_WithABlankName_Is400_AndOfAnUnknownRole_Is404()
    {
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var own = await AddRoleAsync("Ours", companyId);

        (await client.PatchAsJsonAsync($"{Prefix}/{own}", new { Name = "  " })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PatchAsJsonAsync($"{Prefix}/999999", new { Name = "X" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_OfTheRoleOfAnotherCompany_Is404_AndChangesNothing()
    {
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var other = await AddCompanyAsync("Other");
        var theirs = await AddRoleAsync("Theirs", other);

        (await client.PatchAsJsonAsync($"{Prefix}/{theirs}", new { Name = "Mine now" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().SingleAsync(r => r.Id == theirs))).Name.ShouldBe("Theirs");
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_RemovesARoleOfTheCompanyWithItsAccessRights_AndNoOthers()
    {
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var item = await AddMenuItemAsync("Jobs");
        var own = await AddRoleAsync("Ours", companyId);
        var other = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(own, item, companyId: companyId);
        await AddGrantAsync(other, item);

        var response = await client.DeleteAsync($"{Prefix}/{own}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().AnyAsync(r => r.Id == own))).ShouldBeFalse();
        (await RowsOfAsync(own)).ShouldBeEmpty();
        (await RowsOfAsync(other)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Delete_OfARoleThatUsersStillHave_Is409_AndChangesNothing()
    {
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var own = await AddRoleAsync("Ours", companyId);
        await AddMemberAsync(companyId, own, "Some One");

        (await client.DeleteAsync($"{Prefix}/{own}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().AnyAsync(r => r.Id == own))).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_OfASystemRole_Is409_AndOfAnUnknownRole_Is404_AndTheOverrideSurvives()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var system = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(system, seed.ChildA, companyId: companyId);

        (await client.DeleteAsync($"{Prefix}/{system}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.DeleteAsync($"{Prefix}/999999")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().AnyAsync(r => r.Id == system))).ShouldBeTrue();
        (await CompanyRowsOfAsync(system, companyId)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Delete_OfTheRoleOfAnotherCompany_Is404_AndChangesNothing()
    {
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var other = await AddCompanyAsync("Other");
        var theirs = await AddRoleAsync("Theirs", other);

        (await client.DeleteAsync($"{Prefix}/{theirs}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().AnyAsync(r => r.Id == theirs))).ShouldBeTrue();
    }

    // ---- reading the access rights ----

    [Fact]
    public async Task GetMenuPermissions_OfASystemRoleWithoutAnOverride_ShowsTheDefaults()
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
    public async Task GetMenuPermissions_OfAnOwnRole_ShowsItsRows()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var ours = await AddRoleAsync("Ours", companyId);
        await AddGrantAsync(ours, seed.ChildB, edit: true, companyId: companyId);

        var result = await client.GetFromJsonAsync<PermissionsRow>($"{Prefix}/{ours}/menu-permissions");

        result!.IsSystem.ShouldBeFalse();
        var tasks = result.Items.Single().Children.Single(c => c.Title == "Tasks");
        (tasks.CanView, tasks.CanEdit).ShouldBe((true, true));
        result.Items.Single().Children.Single(c => c.Title == "Calendar").CanView.ShouldBeFalse();
    }

    [Fact]
    public async Task GetMenuPermissions_ForAnUnknownRole_Is404()
    {
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);

        (await client.GetAsync($"{Prefix}/999999/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheMenuPermissionsEndpoints_ForTheRoleOfAnotherCompany_Are404_AndChangeNothing()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var other = await AddCompanyAsync("Other");
        var theirs = await AddRoleAsync("Theirs", other);
        await AddGrantAsync(theirs, seed.ChildA, companyId: other);

        (await client.GetAsync($"{Prefix}/{theirs}/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync($"{Prefix}/{theirs}/menu-permissions", Put((seed.ChildB, true, false, false, false)))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"{Prefix}/{theirs}/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await RowsOfAsync(theirs)).Select(g => g.ApplicationMenuItemId).ShouldBe([seed.ChildA]);
    }

    // ---- writing: an own role ----

    [Fact]
    public async Task Put_OnAnOwnRole_StoresEveryMenuItemAsRowsOfTheCompany_AndAnyTickedFlagImpliesView()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var ours = await AddRoleAsync("Ours", companyId);

        var response = await client.PutAsJsonAsync($"{Prefix}/{ours}/menu-permissions", Put(
            (seed.ChildA, true, false, false, false),
            (seed.ChildB, false, true, true, true)));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var rows = await RowsOfAsync(ours);
        rows.Count.ShouldBe(3);
        rows.ShouldAllBe(r => r.CompanyId == companyId);
        var group = rows.Single(r => r.ApplicationMenuItemId == seed.Group);
        (group.CanView, group.CanCreate, group.CanEdit, group.CanDelete).ShouldBe((false, false, false, false));
        var viewOnly = rows.Single(r => r.ApplicationMenuItemId == seed.ChildA);
        (viewOnly.CanView, viewOnly.CanCreate, viewOnly.CanEdit, viewOnly.CanDelete).ShouldBe((true, false, false, false));
        var all = rows.Single(r => r.ApplicationMenuItemId == seed.ChildB);
        (all.CanView, all.CanCreate, all.CanEdit, all.CanDelete).ShouldBe((true, true, true, true));
    }

    [Fact]
    public async Task Put_ReplacesWhatTheRoleHad_ItemsLeftOutAreTakenAway()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var ours = await AddRoleAsync("Ours", companyId);
        await AddGrantAsync(ours, seed.Group, companyId: companyId);
        await AddGrantAsync(ours, seed.ChildA, edit: true, companyId: companyId);

        var response = await client.PutAsJsonAsync($"{Prefix}/{ours}/menu-permissions", Put(
            (seed.ChildA, true, false, false, false),
            (seed.ChildB, true, false, false, false)));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var rows = await RowsOfAsync(ours);
        rows.Where(r => r.CanView == true).Select(r => r.ApplicationMenuItemId).ShouldBe([seed.ChildA, seed.ChildB]);
        rows.Single(r => r.ApplicationMenuItemId == seed.Group).CanView.ShouldBe(false);
        rows.Single(r => r.ApplicationMenuItemId == seed.ChildA).CanEdit.ShouldBe(false);
    }

    [Fact]
    public async Task Put_ThenTheRolesTree_FollowsTheNewFlags()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var ours = await AddRoleAsync("Ours", companyId);

        await client.PutAsJsonAsync($"{Prefix}/{ours}/menu-permissions", Put((seed.ChildB, false, false, true, false)));

        var result = await client.GetFromJsonAsync<PermissionsRow>($"{Prefix}/{ours}/menu-permissions");
        var tasks = result!.Items.Single().Children.Single(c => c.Title == "Tasks");
        (tasks.CanView, tasks.CanEdit).ShouldBe((true, true));
    }

    [Fact]
    public async Task Put_WithAnItemThatIsNotInTheMenu_Is400_AndChangesNothing_NotEvenAnOverride()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(dispatcher, seed.ChildA);

        var response = await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildB, true, false, false, false), (999999, true, false, false, false)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RowsOfAsync(dispatcher)).Select(r => (r.CompanyId, r.ApplicationMenuItemId)).ShouldBe([((int?)null, seed.ChildA)]);
    }

    [Fact]
    public async Task Put_WithTheSameItemTwice_Is400_AndChangesNothing()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var ours = await AddRoleAsync("Ours", companyId);

        var response = await client.PutAsJsonAsync($"{Prefix}/{ours}/menu-permissions", Put((seed.ChildA, true, false, false, false), (seed.ChildA, true, true, false, false)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RowsOfAsync(ours)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_ForAnUnknownRole_Is404()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);

        (await client.PutAsJsonAsync($"{Prefix}/999999/menu-permissions", Put((seed.ChildA, true, false, false, false)))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- writing: overriding a system role ----

    [Fact]
    public async Task Put_OnASystemRole_StoresAFullSetForTheCompany_AndLeavesTheDefaultsUntouched()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(dispatcher, seed.Group);
        await AddGrantAsync(dispatcher, seed.ChildB, edit: true);
        var before = await DefaultRowsOfAsync(dispatcher);

        var response = await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildA, true, false, false, false)));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var own = await CompanyRowsOfAsync(dispatcher, companyId);
        own.Select(g => (g.ApplicationMenuItemId, g.CanView)).ShouldBe([(seed.Group, (bool?)false), (seed.ChildA, true), (seed.ChildB, false)]);
        var after = await DefaultRowsOfAsync(dispatcher);
        after.Select(g => (g.Id, g.ApplicationMenuItemId, g.CanView, g.CanEdit)).ShouldBe(before.Select(g => (g.Id, g.ApplicationMenuItemId, g.CanView, g.CanEdit)));
    }

    [Fact]
    public async Task Put_OnASystemRole_FlipsIsOverridden_InTheListAndInTheTree()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(dispatcher, seed.ChildB);
        (await RoleSummaryAsync(client, dispatcher)).IsOverridden.ShouldBeFalse();
        (await client.GetFromJsonAsync<PermissionsRow>($"{Prefix}/{dispatcher}/menu-permissions"))!.IsOverridden.ShouldBeFalse();

        await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildA, true, false, true, false)));

        var summary = await RoleSummaryAsync(client, dispatcher);
        summary.IsOverridden.ShouldBeTrue();
        summary.IsSystem.ShouldBeTrue();
        summary.ViewCount.ShouldBe(1);
        summary.EditCount.ShouldBe(1);
        (await client.GetFromJsonAsync<PermissionsRow>($"{Prefix}/{dispatcher}/menu-permissions"))!.IsOverridden.ShouldBeTrue();
    }

    [Fact]
    public async Task Put_OnASystemRole_ShowsInTheTree_AndAnotherCompanyStillSeesTheDefaults()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var (otherClient, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");
        await AddGrantAsync(dispatcher, seed.ChildB);

        await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildA, true, false, false, false)));

        var mine = await client.GetFromJsonAsync<PermissionsRow>($"{Prefix}/{dispatcher}/menu-permissions");
        mine!.IsOverridden.ShouldBeTrue();
        mine.Items.Single().Children.Select(c => (c.Title, c.CanView)).ShouldBe([("Calendar", true), ("Tasks", false)]);

        var theirs = await otherClient.GetFromJsonAsync<PermissionsRow>($"{Prefix}/{dispatcher}/menu-permissions");
        theirs!.IsOverridden.ShouldBeFalse();
        theirs.Items.Single().Children.Select(c => (c.Title, c.CanView)).ShouldBe([("Calendar", false), ("Tasks", true)]);
        (await RoleSummaryAsync(otherClient, dispatcher)).IsOverridden.ShouldBeFalse();
    }

    [Fact]
    public async Task Put_OnASystemRoleAgain_ReplacesTheOverride_AndNeverDuplicatesRows()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");

        await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildA, true, false, false, false)));
        await client.PutAsJsonAsync($"{Prefix}/{dispatcher}/menu-permissions", Put((seed.ChildB, true, false, false, false)));

        var own = await CompanyRowsOfAsync(dispatcher, companyId);
        own.Count.ShouldBe(3);
        own.Where(g => g.CanView == true).Select(g => g.ApplicationMenuItemId).ShouldBe([seed.ChildB]);
    }

    [Fact]
    public async Task AnOverrideThatAllowsNothing_StillHidesEverything_InsteadOfFallingBackToTheDefaults()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var administrator = await RoleIdAsync("Administrator");
        await AddGrantAsync(administrator, seed.ChildA);
        (await MenuTitlesAsync(client)).ShouldBe(["Jobs", "Calendar"]);

        var response = await client.PutAsJsonAsync($"{Prefix}/{administrator}/menu-permissions", Put());

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CompanyRowsOfAsync(administrator, companyId)).Count.ShouldBe(3);
        (await MenuTitlesAsync(client)).ShouldBeEmpty();
        var summary = await RoleSummaryAsync(client, administrator);
        summary.IsOverridden.ShouldBeTrue();
        summary.ViewCount.ShouldBe(0);
    }

    // ---- going back to the defaults ----

    [Fact]
    public async Task DeleteMenuPermissions_RemovesOnlyTheCompanysRows_AndTheRoleGoesBackToTheDefaults()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var other = await AddCompanyAsync("Other");
        var dispatcher = await AddRoleAsync("Dispatcher");
        var billing = await AddRoleAsync("Billing");
        await AddGrantAsync(dispatcher, seed.ChildB);
        await AddGrantAsync(dispatcher, seed.ChildA, companyId: companyId);
        await AddGrantAsync(dispatcher, seed.ChildA, companyId: other);
        await AddGrantAsync(billing, seed.ChildA, companyId: companyId);

        var response = await client.DeleteAsync($"{Prefix}/{dispatcher}/menu-permissions");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CompanyRowsOfAsync(dispatcher, companyId)).ShouldBeEmpty();
        (await CompanyRowsOfAsync(dispatcher, other)).Count.ShouldBe(1);
        (await DefaultRowsOfAsync(dispatcher)).Select(g => g.ApplicationMenuItemId).ShouldBe([seed.ChildB]);
        (await CompanyRowsOfAsync(billing, companyId)).Count.ShouldBe(1);
        var summary = await RoleSummaryAsync(client, dispatcher);
        summary.IsOverridden.ShouldBeFalse();
        summary.ViewCount.ShouldBe(1);
        var tree = await client.GetFromJsonAsync<PermissionsRow>($"{Prefix}/{dispatcher}/menu-permissions");
        tree!.Items.Single().Children.Select(c => (c.Title, c.CanView)).ShouldBe([("Calendar", false), ("Tasks", true)]);
    }

    [Fact]
    public async Task DeleteMenuPermissions_WithNothingToDrop_Is204_AndOfAnOwnRoleLeavesItWithNone()
    {
        var seed = await SeedMenuAsync();
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var dispatcher = await AddRoleAsync("Dispatcher");
        var ours = await AddRoleAsync("Ours", companyId);
        await AddGrantAsync(dispatcher, seed.ChildA);
        await AddGrantAsync(ours, seed.ChildA, companyId: companyId);

        (await client.DeleteAsync($"{Prefix}/{dispatcher}/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"{Prefix}/{ours}/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await DefaultRowsOfAsync(dispatcher)).Count.ShouldBe(1);
        (await RowsOfAsync(ours)).ShouldBeEmpty();
        (await _fixture.QueryDbAsync(db => db.Set<PermissionRole>().AnyAsync(r => r.Id == ours))).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteMenuPermissions_ForAnUnknownRole_Is404()
    {
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);

        (await client.DeleteAsync($"{Prefix}/999999/menu-permissions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- what it does to the menu ----

    [Fact]
    public async Task TheMenu_FollowsTheCompanysOverride_AndGoesBackToTheDefaultsWhenItIsDropped()
    {
        var seed = await SeedMenuAsync();
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var (otherClient, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var administrator = await RoleIdAsync("Administrator");
        await AddGrantAsync(administrator, seed.ChildA);

        (await MenuTitlesAsync(client)).ShouldBe(["Jobs", "Calendar"]);

        await client.PutAsJsonAsync($"{Prefix}/{administrator}/menu-permissions", Put((seed.ChildB, true, false, true, false)));

        (await MenuTitlesAsync(client)).ShouldBe(["Jobs", "Tasks"]);
        Flatten((await client.GetFromJsonAsync<List<MenuRow>>("/api/menu"))!).Single(i => i.Title == "Tasks").CanEdit.ShouldBeTrue();
        (await MenuTitlesAsync(otherClient)).ShouldBe(["Jobs", "Calendar"]);

        await client.DeleteAsync($"{Prefix}/{administrator}/menu-permissions");

        (await MenuTitlesAsync(client)).ShouldBe(["Jobs", "Calendar"]);
    }

    // ---- users ----

    [Fact]
    public async Task GetUsers_ListsTheCompanysUsersWithTheirRoles_MarksTheCaller_AndLeavesOutDeletedOnes()
    {
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
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
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var otherCompany = await AddCompanyAsync("Other");
        var role = await AddRoleAsync("Dispatcher");
        await AddMemberAsync(otherCompany, role, "Far Away");

        (await client.GetFromJsonAsync<List<UserRow>>($"{Prefix}/users"))!.Select(u => u.DisplayName).ShouldNotContain("Far Away");
    }

    // ---- giving users a role ----

    [Fact]
    public async Task Assign_GivesTheUsersTheRole_ReplacingTheirOldOne_AndLeavesOthersAlone()
    {
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
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
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
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
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
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
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var billing = await AddRoleAsync("Billing");
        var dispatcher = await AddRoleAsync("Dispatcher");
        var locked = await AddMemberAsync(companyId, dispatcher, "Locked Out", User2CompanyStatus.SuspendedBySiteAdmin);

        (await client.PostAsJsonAsync($"{Prefix}/{billing}/members", new { UserIds = new[] { locked } })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await RoleOfAsync(companyId, locked)).ShouldBe(dispatcher);
    }

    [Fact]
    public async Task Assign_ToAnUnknownRole_Is404_AndNoUsersIsFine()
    {
        var (client, _) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var billing = await AddRoleAsync("Billing");

        (await client.PostAsJsonAsync($"{Prefix}/999999/members", new { UserIds = new[] { 1 } })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync($"{Prefix}/{billing}/members", new { UserIds = Array.Empty<int>() })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Assign_ToTheRoleOfAnotherCompany_Is404_AndChangesNothing()
    {
        var (client, companyId) = await CompanyTestClients.CreateAdministratorAsync(_fixture);
        var other = await AddCompanyAsync("Other");
        var theirs = await AddRoleAsync("Theirs", other);
        var dispatcher = await AddRoleAsync("Dispatcher");
        var anna = await AddMemberAsync(companyId, dispatcher, "Anna One");

        (await client.PostAsJsonAsync($"{Prefix}/{theirs}/members", new { UserIds = new[] { anna } })).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await RoleOfAsync(companyId, anna)).ShouldBe(dispatcher);
    }
}
