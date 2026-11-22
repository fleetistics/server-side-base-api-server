using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using api_server.Auth;
using api_server.core;
using Microsoft.AspNetCore.Authorization;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// RoleAuthorizationHandler: who passes a role policy. The lookup is a fake, so only the handler's own decisions are tested
/// (which claims it needs, what it asks, and that anything but a yes is a refusal); the lookup's query is covered by
/// MessageTemplatesAdminControllerTests against a real database.
/// </summary>
public sealed class RoleAuthorizationHandlerTests
{
    private sealed class FakeRoles(bool answer) : IRoleLookup
    {
        public List<(int UserId, int CompanyId, string RoleName)> Asked { get; } = [];

        public Task<bool> HasRoleAsync(int userId, int companyId, string roleName, CancellationToken cancellationToken)
        {
            Asked.Add((userId, companyId, roleName));
            return Task.FromResult(answer);
        }
    }

    private static async Task<(bool Succeeded, FakeRoles Roles)> RunAsync(bool lookupAnswer, params Claim[] claims)
    {
        var roles = new FakeRoles(lookupAnswer);
        var requirement = new RoleRequirement(AuthPolicies.PortalAdminRole);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);

        await new RoleAuthorizationHandler(roles).HandleAsync(context);

        return (context.HasSucceeded, roles);
    }

    private static Claim User(string id) => new(JwtRegisteredClaimNames.Sub, id);
    private static Claim Company(string id) => new(TokenService.CompanyIdClaim, id);

    [Fact]
    public async Task ACallerWhoHasTheRoleInTheirCompany_Passes_AndTheLookupIsAskedForExactlyThat()
    {
        var (succeeded, roles) = await RunAsync(true, User("28"), Company("1"));

        succeeded.ShouldBeTrue();
        roles.Asked.ShouldBe([(28, 1, "Portal Admin")]);
    }

    [Fact]
    public async Task ACallerWhoDoesNotHaveTheRole_IsRefused()
    {
        var (succeeded, roles) = await RunAsync(false, User("26"), Company("1"));

        succeeded.ShouldBeFalse();
        roles.Asked.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ACallerWithoutACompany_IsRefused_WithoutAskingTheLookup()
    {
        var (succeeded, roles) = await RunAsync(true, User("28"));

        succeeded.ShouldBeFalse();
        roles.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACallerWithoutAUserId_IsRefused_WithoutAskingTheLookup()
    {
        var (succeeded, roles) = await RunAsync(true, Company("1"));

        succeeded.ShouldBeFalse();
        roles.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnIdThatIsNotANumber_IsRefused_WithoutAskingTheLookup()
    {
        (await RunAsync(true, User("abc"), Company("1"))).Succeeded.ShouldBeFalse();
        (await RunAsync(true, User("28"), Company("one"))).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task ARoleClaimInTheToken_IsNotEnough()
    {
        // The token's own claims never grant the role: only the database does.
        var (succeeded, _) = await RunAsync(false, User("28"), Company("1"), new Claim(ClaimTypes.Role, "Portal Admin"), new Claim("role", "Portal Admin"));

        succeeded.ShouldBeFalse();
    }
}
