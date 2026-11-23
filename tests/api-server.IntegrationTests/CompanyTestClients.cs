using System.Net.Http.Headers;
using System.Net.Http.Json;
using db_model.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace api_server.IntegrationTests;

/// <summary>A client signed in as the seeded user, working in a company where they hold a given system-wide role.</summary>
internal static class CompanyTestClients
{
    /// <summary>
    /// Logs in as the seeded user, creates a company and gives them the system-wide role with this name in it (the role is created if
    /// it does not exist), and returns a client whose access token names that company, as a login would.
    /// </summary>
    public static async Task<(HttpClient Client, int CompanyId)> CreateAsync(ApiFixture fixture, string roleName)
    {
        var client = fixture.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<ApiFixture.LoginResponse>();

        var companyId = await fixture.QueryDbAsync(async db =>
        {
            var company = new Company { Name = "Test Company", StatusId = 1 };
            var role = await db.Set<PermissionRole>().FirstOrDefaultAsync(r => r.Name == roleName && r.CompanyId == null)
                ?? db.Set<PermissionRole>().Add(new PermissionRole { Name = roleName, CompanyId = null }).Entity;
            db.Set<Company>().Add(company);
            await db.SaveChangesAsync();
            db.Set<User2Company>().Add(new User2Company
            {
                CompanyId = company.Id,
                UserId = fixture.TestUserId,
                PermissionRoleId = role.Id,
                StatusId = User2CompanyStatus.Active,
                JoinTypeId = 1,
                JoinedDate = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return company.Id;
        });

        var tokens = fixture.Services.GetRequiredService<api_server.Auth.TokenService>();
        var token = tokens.CreateAccessToken(session!.UserId.ToString(), session.SessionId.ToString(), companyId: companyId.ToString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, companyId);
    }

    public static Task<(HttpClient Client, int CompanyId)> CreateAdministratorAsync(ApiFixture fixture) => CreateAsync(fixture, "Administrator");
}
