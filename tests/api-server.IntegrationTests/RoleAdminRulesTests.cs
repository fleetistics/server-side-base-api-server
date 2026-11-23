using api_server.Controllers.Roles.Services;
using db_model.Companies;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>The rules for creating and renaming roles (RoleAdminRules): no database.</summary>
public sealed class RoleAdminRulesTests
{
    private static PermissionRole Role(int id, string name, int? companyId = null) => new() { Id = id, Name = name, CompanyId = companyId };

    [Theory]
    [InlineData("Dispatcher", "Dispatcher")]
    [InlineData("  Regional Dispatcher  ", "Regional Dispatcher")]
    [InlineData("\tBilling\n", "Billing")]
    public void ANameIsTrimmed(string input, string expected)
    {
        RoleAdminRules.NormalizeName(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankNameIsNoName(string? input)
    {
        RoleAdminRules.NormalizeName(input).ShouldBeNull();
        RoleAdminRules.IsValidName(RoleAdminRules.NormalizeName(input)).ShouldBeFalse();
    }

    [Fact]
    public void ANameOfTheLongestAllowedLength_IsValid_AndOneMoreIsNot()
    {
        RoleAdminRules.IsValidName(new string('a', RoleAdminRules.MaxNameLength)).ShouldBeTrue();
        RoleAdminRules.IsValidName(new string('a', RoleAdminRules.MaxNameLength + 1)).ShouldBeFalse();
    }

    [Fact]
    public void ADescriptionIsTrimmed_AndEmptyIsNull()
    {
        RoleAdminRules.NormalizeDescription("  Can dispatch  ").ShouldBe("Can dispatch");
        RoleAdminRules.NormalizeDescription("   ").ShouldBeNull();
        RoleAdminRules.NormalizeDescription(null).ShouldBeNull();
    }

    [Fact]
    public void ANameIsTaken_IgnoringCase_ByAnyRoleTheCompanyCanUse()
    {
        PermissionRole[] roles = [Role(1, "Administrator"), Role(2, "Our Dispatcher", companyId: 5)];

        RoleAdminRules.IsNameTaken(roles, "administrator").ShouldBeTrue();
        RoleAdminRules.IsNameTaken(roles, "OUR DISPATCHER").ShouldBeTrue();
        RoleAdminRules.IsNameTaken(roles, "Technician").ShouldBeFalse();
    }

    [Fact]
    public void ARoleMayKeepItsOwnName_WhenItIsRenamed()
    {
        PermissionRole[] roles = [Role(1, "Administrator"), Role(2, "Our Dispatcher", companyId: 5)];

        RoleAdminRules.IsNameTaken(roles, "our dispatcher", exceptRoleId: 2).ShouldBeFalse();
        RoleAdminRules.IsNameTaken(roles, "Administrator", exceptRoleId: 2).ShouldBeTrue();
    }

    [Fact]
    public void OnlyDeletedMembershipsAreGone_AndOnlyTheSiteAdminsSuspensionIsLocked()
    {
        RoleAdminRules.IsDeleted(User2CompanyStatus.Deleted).ShouldBeTrue();
        RoleAdminRules.IsDeleted(User2CompanyStatus.DeletedBySiteAdmin).ShouldBeTrue();
        RoleAdminRules.IsDeleted(User2CompanyStatus.Active).ShouldBeFalse();
        RoleAdminRules.IsDeleted(User2CompanyStatus.Suspended).ShouldBeFalse();

        RoleAdminRules.IsLockedBySiteAdmin(User2CompanyStatus.SuspendedBySiteAdmin).ShouldBeTrue();
        RoleAdminRules.IsLockedBySiteAdmin(User2CompanyStatus.Suspended).ShouldBeFalse();
    }
}
