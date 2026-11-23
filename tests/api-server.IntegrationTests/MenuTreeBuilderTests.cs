using api_server.Controllers.Menu.Dto;
using api_server.Controllers.Menu.Services;
using db_model.AppStructure;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>The tree a role sees (MenuTreeBuilder): no database, only rows in and a tree out.</summary>
public sealed class MenuTreeBuilderTests
{
    private static ApplicationMenuItem Item(int id, string title, int? parentId = null, short? order = null, string? path = null) =>
        new() { Id = id, MenuId = 1, Title = title, ParentId = parentId, OrderBy = order, Path = path };

    private static Role2MenuItem Grant(int itemId, bool? view = true, bool? edit = null, bool? create = null, bool? delete = null) =>
        new() { PermissionRoleId = 7, ApplicationMenuItemId = itemId, CanView = view, CanEdit = edit, CanCreate = create, CanDelete = delete };

    private static string Shape(IEnumerable<MenuItemDto> items) =>
        string.Join(",", items.Select(i => i.Children.Count == 0 ? i.Title : $"{i.Title}[{Shape(i.Children)}]"));

    private static readonly ApplicationMenuItem[] Sample =
    [
        Item(1, "Home", order: 1, path: "/home"),
        Item(2, "Jobs", order: 2, path: "/jobs"),
        Item(3, "Calendar", parentId: 2, order: 1, path: "/jobs/schedule"),
        Item(4, "Tasks", parentId: 2, order: 2, path: "/jobs/tasks"),
        Item(5, "Reports", order: 3),
        Item(6, "All reports", parentId: 5, order: 1, path: "/reports/all"),
    ];

    [Fact]
    public void NoGrants_GivesAnEmptyMenu()
    {
        MenuTreeBuilder.Build(Sample, []).ShouldBeEmpty();
    }

    [Fact]
    public void AGrantWithoutCanView_IsNotAGrant()
    {
        MenuTreeBuilder.Build(Sample, [Grant(1, view: false), Grant(3, view: null)]).ShouldBeEmpty();
    }

    [Fact]
    public void AGrantForAnItemOfAnotherMenu_IsIgnored()
    {
        MenuTreeBuilder.Build(Sample, [Grant(999)]).ShouldBeEmpty();
    }

    [Fact]
    public void AGrantedLeaf_BringsItsGroupsAlongWithoutAnyRights()
    {
        var tree = MenuTreeBuilder.Build(Sample, [Grant(3, edit: true, create: true, delete: true)]);

        Shape(tree).ShouldBe("Jobs[Calendar]");
        var jobs = tree.Single();
        jobs.CanEdit.ShouldBeFalse();
        jobs.CanCreate.ShouldBeFalse();
        jobs.CanDelete.ShouldBeFalse();
        var calendar = jobs.Children.Single();
        calendar.CanEdit.ShouldBeTrue();
        calendar.CanCreate.ShouldBeTrue();
        calendar.CanDelete.ShouldBeTrue();
    }

    [Fact]
    public void ARightsFlagThatIsNull_IsFalse()
    {
        var tree = MenuTreeBuilder.Build(Sample, [Grant(1)]);

        var home = tree.Single();
        home.CanEdit.ShouldBeFalse();
        home.CanCreate.ShouldBeFalse();
        home.CanDelete.ShouldBeFalse();
        home.DividerBefore.ShouldBeFalse();
        home.DividerAfter.ShouldBeFalse();
    }

    [Fact]
    public void AGrantedGroup_StaysWhenNothingBelowItIsGranted_AndKeepsItsOwnRights()
    {
        var tree = MenuTreeBuilder.Build(Sample, [Grant(2, edit: true)]);

        Shape(tree).ShouldBe("Jobs");
        tree.Single().Children.ShouldBeEmpty();
        tree.Single().CanEdit.ShouldBeTrue();
    }

    [Fact]
    public void ASiblingTheRoleMayNotView_IsLeftOut()
    {
        Shape(MenuTreeBuilder.Build(Sample, [Grant(4)])).ShouldBe("Jobs[Tasks]");
    }

    [Fact]
    public void SiblingsAreInOrderByOrderThenId_AndItemsWithoutAnOrderComeLast()
    {
        ApplicationMenuItem[] items =
        [
            Item(10, "NoOrderB"),
            Item(11, "Second", order: 2),
            Item(12, "First", order: 1),
            Item(9, "NoOrderA"),
            Item(13, "AlsoSecond", order: 2),
        ];

        var tree = MenuTreeBuilder.Build(items, items.Select(i => Grant(i.Id)).ToList());

        Shape(tree).ShouldBe("First,Second,AlsoSecond,NoOrderA,NoOrderB");
    }

    [Fact]
    public void ADeepTree_IsBuiltLevelByLevel_InTheOrderOfTheRows()
    {
        ApplicationMenuItem[] items =
        [
            Item(1, "A", order: 1),
            Item(2, "A2", parentId: 1, order: 2),
            Item(3, "A1", parentId: 1, order: 1),
            Item(4, "A1x", parentId: 3, order: 1),
            Item(5, "B", order: 2),
        ];

        var tree = MenuTreeBuilder.Build(items, items.Select(i => Grant(i.Id)).ToList());

        Shape(tree).ShouldBe("A[A1[A1x],A2],B");
    }

    [Fact]
    public void AParentThatIsNotInTheMenu_MakesTheItemATopLevelOne()
    {
        ApplicationMenuItem[] items = [Item(1, "Lost", parentId: 404, order: 1)];

        Shape(MenuTreeBuilder.Build(items, [Grant(1)])).ShouldBe("Lost");
    }

    [Fact]
    public void ACycleInTheData_DoesNotHangAndDoesNotBreakTheOtherItems()
    {
        ApplicationMenuItem[] items =
        [
            Item(1, "Cycle1", parentId: 2),
            Item(2, "Cycle2", parentId: 1),
            Item(3, "Fine", order: 1),
        ];

        var tree = MenuTreeBuilder.Build(items, items.Select(i => Grant(i.Id)).ToList());

        Shape(tree).ShouldBe("Fine");
    }

    [Fact]
    public void TheWholeSampleForARoleThatMayViewEverything_KeepsTheShapeOfTheMenu()
    {
        var tree = MenuTreeBuilder.Build(Sample, Sample.Select(i => Grant(i.Id, edit: true)).ToList());

        Shape(tree).ShouldBe("Home,Jobs[Calendar,Tasks],Reports[All reports]");
        tree.Single(i => i.Title == "Reports").Path.ShouldBeNull();
        tree.Single(i => i.Title == "Jobs").Path.ShouldBe("/jobs");
    }
}
