using api_server.Controllers.Roles.Dto;
using api_server.Controllers.Roles.Services;
using db_model.AppStructure;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>What a request for a role's menu permissions means, and the tree the Roles screen shows (RoleMenuPermissionRules): no database.</summary>
public sealed class RoleMenuPermissionRulesTests
{
    private static RoleMenuItemGrantDto Grant(int itemId, bool view = false, bool create = false, bool edit = false, bool delete = false) =>
        new() { ApplicationMenuItemId = itemId, CanView = view, CanCreate = create, CanEdit = edit, CanDelete = delete };

    private static ApplicationMenuItem Item(int id, string title, int? parentId = null, short? order = null, string? path = null) =>
        new() { Id = id, MenuId = 1, Title = title, ParentId = parentId, OrderBy = order, Path = path };

    private static Role2MenuItem Row(int itemId, bool view = false, bool create = false, bool edit = false, bool delete = false) =>
        new() { PermissionRoleId = 7, ApplicationMenuItemId = itemId, CanView = view, CanCreate = create, CanEdit = edit, CanDelete = delete };

    private static string Shape(IEnumerable<RoleMenuItemPermissionDto> items) =>
        string.Join(",", items.Select(i => i.Children.Count == 0 ? i.Title : $"{i.Title}[{Shape(i.Children)}]"));

    // ---- Normalize ----

    [Fact]
    public void Create_Edit_AndDelete_EachImplyView()
    {
        var result = RoleMenuPermissionRules.Normalize([Grant(1, create: true), Grant(2, edit: true), Grant(3, delete: true)]);

        result.Select(g => g.ApplicationMenuItemId).ShouldBe([1, 2, 3]);
        result.ShouldAllBe(g => g.CanView);
        result[0].CanCreate.ShouldBeTrue();
        result[0].CanEdit.ShouldBeFalse();
        result[1].CanEdit.ShouldBeTrue();
        result[2].CanDelete.ShouldBeTrue();
    }

    [Fact]
    public void ViewOnly_StaysViewOnly()
    {
        var result = RoleMenuPermissionRules.Normalize([Grant(1, view: true)]).Single();

        result.CanView.ShouldBeTrue();
        result.CanCreate.ShouldBeFalse();
        result.CanEdit.ShouldBeFalse();
        result.CanDelete.ShouldBeFalse();
    }

    [Fact]
    public void AnEntryWithNothingTicked_IsNoEntry()
    {
        RoleMenuPermissionRules.Normalize([Grant(1), Grant(2, view: true), Grant(3)]).Select(g => g.ApplicationMenuItemId).ShouldBe([2]);
    }

    [Fact]
    public void NoEntries_GiveNoEntries()
    {
        RoleMenuPermissionRules.Normalize([]).ShouldBeEmpty();
    }

    // ---- Effective / HasCompanyRows ----

    private static Role2MenuItem CompanyRow(int itemId, int? companyId, bool view = false) =>
        new() { PermissionRoleId = 7, ApplicationMenuItemId = itemId, CompanyId = companyId, CanView = view };

    [Fact]
    public void Effective_WithoutRowsOfTheCompany_IsTheDefaults()
    {
        var rows = new[] { CompanyRow(1, null, true), CompanyRow(2, null, true), CompanyRow(1, 99, true) };

        RoleMenuPermissionRules.Effective(rows, 5).Select(r => r.ApplicationMenuItemId).ShouldBe([1, 2]);
    }

    [Fact]
    public void Effective_WithRowsOfTheCompany_IsThoseRowsOnly_NotMergedWithTheDefaults()
    {
        var rows = new[] { CompanyRow(1, null, true), CompanyRow(2, null, true), CompanyRow(3, 5, true), CompanyRow(1, 99, true) };

        var result = RoleMenuPermissionRules.Effective(rows, 5);

        result.Select(r => r.ApplicationMenuItemId).ShouldBe([3]);
        result.ShouldAllBe(r => r.CompanyId == 5);
    }

    [Fact]
    public void Effective_AnOverrideThatAllowsNothing_StillWins_AndIsNotTheDefaults()
    {
        var rows = new[] { CompanyRow(1, null, true), CompanyRow(1, 5, false), CompanyRow(2, 5, false) };

        var result = RoleMenuPermissionRules.Effective(rows, 5);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(r => r.CanView == false);
    }

    [Fact]
    public void Effective_WithNoCompany_IsTheDefaultsOnly()
    {
        var rows = new[] { CompanyRow(1, null, true), CompanyRow(2, 5, true) };

        RoleMenuPermissionRules.Effective(rows, null).Select(r => r.ApplicationMenuItemId).ShouldBe([1]);
    }

    [Fact]
    public void Effective_WithNoRows_IsEmpty()
    {
        RoleMenuPermissionRules.Effective([], 5).ShouldBeEmpty();
        RoleMenuPermissionRules.Effective([], null).ShouldBeEmpty();
    }

    [Fact]
    public void HasCompanyRows_IsTrueOnlyForRowsOfThatCompany()
    {
        var rows = new[] { CompanyRow(1, null, true), CompanyRow(2, 99, true), CompanyRow(3, 5) };

        RoleMenuPermissionRules.HasCompanyRows(rows, 5).ShouldBeTrue();
        RoleMenuPermissionRules.HasCompanyRows(rows, 99).ShouldBeTrue();
        RoleMenuPermissionRules.HasCompanyRows(rows, 6).ShouldBeFalse();
        RoleMenuPermissionRules.HasCompanyRows([CompanyRow(1, null, true)], 5).ShouldBeFalse();
        RoleMenuPermissionRules.HasCompanyRows([], 5).ShouldBeFalse();
    }

    // ---- FullSet ----

    [Fact]
    public void FullSet_HasEveryMenuItemInTheOrderGiven_WithAllFalseForWhatIsNotRequested()
    {
        var result = RoleMenuPermissionRules.FullSet([3, 1, 2], [Grant(2, view: true)]);

        result.Select(g => g.ApplicationMenuItemId).ShouldBe([3, 1, 2]);
        result.Where(g => g.CanView).Select(g => g.ApplicationMenuItemId).ShouldBe([2]);
        var unlisted = result.Single(g => g.ApplicationMenuItemId == 3);
        (unlisted.CanView, unlisted.CanCreate, unlisted.CanEdit, unlisted.CanDelete).ShouldBe((false, false, false, false));
    }

    [Fact]
    public void FullSet_NormalizesTheRequest_TickedFlagsImplyView_AndAnEmptyEntryIsFalse()
    {
        var result = RoleMenuPermissionRules.FullSet([1, 2, 3], [Grant(1, edit: true), Grant(2)]);

        var first = result.Single(g => g.ApplicationMenuItemId == 1);
        (first.CanView, first.CanEdit).ShouldBe((true, true));
        var second = result.Single(g => g.ApplicationMenuItemId == 2);
        (second.CanView, second.CanCreate, second.CanEdit, second.CanDelete).ShouldBe((false, false, false, false));
        result.Count.ShouldBe(3);
    }

    [Fact]
    public void FullSet_WithNothingRequested_IsEveryItemWithNothingAllowed()
    {
        var result = RoleMenuPermissionRules.FullSet([1, 2], []);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(g => !g.CanView && !g.CanCreate && !g.CanEdit && !g.CanDelete);
    }

    [Fact]
    public void FullSet_IgnoresARequestedItemThatIsNotInTheMenu()
    {
        var result = RoleMenuPermissionRules.FullSet([1], [Grant(1, view: true), Grant(9, view: true)]);

        result.Select(g => g.ApplicationMenuItemId).ShouldBe([1]);
    }

    // ---- BuildTree ----

    private static readonly ApplicationMenuItem[] Menu =
    [
        Item(1, "Home", order: 1, path: "/home"),
        Item(2, "Jobs", order: 2, path: "/jobs"),
        Item(4, "Tasks", parentId: 2, order: 2, path: "/jobs/tasks"),
        Item(3, "Calendar", parentId: 2, order: 1, path: "/jobs/schedule"),
        Item(5, "Reports", order: 3),
    ];

    [Fact]
    public void TheTreeHasEveryItem_InOrder_WhateverTheRoleMayDo()
    {
        var tree = RoleMenuPermissionRules.BuildTree(Menu, new Dictionary<int, Role2MenuItem>());

        Shape(tree).ShouldBe("Home,Jobs[Calendar,Tasks],Reports");
    }

    [Fact]
    public void AnItemWithoutARow_HasEveryFlagFalse()
    {
        var tree = RoleMenuPermissionRules.BuildTree(Menu, new Dictionary<int, Role2MenuItem>());

        foreach (var item in tree)
        {
            item.CanView.ShouldBeFalse();
            item.CanCreate.ShouldBeFalse();
            item.CanEdit.ShouldBeFalse();
            item.CanDelete.ShouldBeFalse();
        }
    }

    [Fact]
    public void TheRolesFlagsAreOnTheirItems_AndANullFlagIsFalse()
    {
        var grants = new Dictionary<int, Role2MenuItem>
        {
            [3] = Row(3, view: true, edit: true),
            [1] = new() { PermissionRoleId = 7, ApplicationMenuItemId = 1, CanView = true },
        };

        var tree = RoleMenuPermissionRules.BuildTree(Menu, grants);

        var calendar = tree.Single(i => i.Title == "Jobs").Children.Single(i => i.Title == "Calendar");
        calendar.CanView.ShouldBeTrue();
        calendar.CanEdit.ShouldBeTrue();
        calendar.CanCreate.ShouldBeFalse();
        calendar.CanDelete.ShouldBeFalse();
        var home = tree.Single(i => i.Title == "Home");
        home.CanView.ShouldBeTrue();
        home.CanEdit.ShouldBeFalse();
        tree.Single(i => i.Title == "Jobs").CanView.ShouldBeFalse();
    }

    [Fact]
    public void ThePathAndIconAreCarried_AndAGroupWithoutAPageHasNoPath()
    {
        ApplicationMenuItem[] menu = [new() { Id = 1, MenuId = 1, Title = "Reports", Icon = "FileText" }];

        var item = RoleMenuPermissionRules.BuildTree(menu, new Dictionary<int, Role2MenuItem>()).Single();

        item.Icon.ShouldBe("FileText");
        item.Path.ShouldBeNull();
    }

    [Fact]
    public void AnItemWhoseParentIsNotInTheMenu_IsATopLevelOne()
    {
        ApplicationMenuItem[] menu = [Item(1, "Lost", parentId: 404, order: 1)];

        Shape(RoleMenuPermissionRules.BuildTree(menu, new Dictionary<int, Role2MenuItem>())).ShouldBe("Lost");
    }
}
