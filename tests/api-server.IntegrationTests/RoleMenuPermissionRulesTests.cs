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
