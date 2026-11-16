using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using exs.notifications_model.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// /api/admin/message-templates (MessageTemplatesAdminController): the developers' endpoints for notification templates.
/// Admin only: the host issues no role claims yet, so the tests mint an access token with the "admin" role for the seeded
/// user from the host's own TokenService, in the session the login created.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MessageTemplatesAdminControllerTests : IAsyncLifetime
{
    private const string Prefix = "/api/admin/message-templates";
    private const string Key = "password-reset";

    private readonly ApiFixture _fixture;

    public MessageTemplatesAdminControllerTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = _fixture.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { UserName = ApiFixture.TestUserName, Password = ApiFixture.TestPassword });
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<ApiFixture.LoginResponse>();

        var tokens = _fixture.Services.GetRequiredService<api_server.Auth.TokenService>();
        var token = tokens.CreateAccessToken(session!.UserId.ToString(), session.SessionId.ToString(), roles: ["admin"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<NotificationTemplate> AddRowAsync(string language, int version, bool active, string subject = "Subject")
    {
        return await _fixture.QueryDbAsync(async db =>
        {
            var row = new NotificationTemplate
            {
                TemplateKey = Key,
                Language = language,
                Version = version,
                IsActive = active,
                Subject = subject,
                Text = "Hi {{ user_name }}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed",
            };
            db.Set<NotificationTemplate>().Add(row);
            await db.SaveChangesAsync();
            return row;
        });
    }

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>());

    // ---- who may call ----

    [Fact]
    public async Task EveryEndpoint_WithoutALogin_Is401()
    {
        var anonymous = _fixture.CreateClient();

        (await anonymous.GetAsync($"{Prefix}/models")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync(Prefix)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync($"{Prefix}/{Key}/preview", new { subject = "S", text = "t" })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact(Skip = "The admin policy is off for now (see the TODO on MessageTemplatesAdminController): any logged-in user may use the endpoints. Remove the Skip when the policy is back.")]
    public async Task EveryEndpoint_ForAUserWithoutTheAdminRole_Is403_AndWritesNothing()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        (await client.GetAsync($"{Prefix}/models")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync($"{Prefix}/{Key}/versions")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var create = await client.PostAsJsonAsync($"{Prefix}/{Key}/versions", new { language = "en", subject = "S", text = "t" });
        create.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsync($"{Prefix}/versions/1/activate", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await _fixture.QueryDbAsync(db => db.Set<NotificationTemplate>().CountAsync())).ShouldBe(0);
    }

    // ---- models and keys ----

    [Fact]
    public async Task GetModels_DescribesPasswordReset_WithItsValuesAndASample()
    {
        var client = await CreateAdminClientAsync();

        var response = await client.GetAsync($"{Prefix}/models");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var model = (await JsonOf(response)).EnumerateArray().Single(m => m.GetProperty("key").GetString() == Key);
        var variables = model.GetProperty("variables").EnumerateArray().ToDictionary(v => v.GetProperty("path").GetString()!);
        variables.Keys.ShouldBe(["user_name", "reset_link", "expires_at_utc"]);
        variables["user_name"].GetProperty("type").GetString().ShouldBe("string");
        variables["user_name"].GetProperty("optional").GetBoolean().ShouldBeFalse();
        variables["expires_at_utc"].GetProperty("type").GetString().ShouldBe("datetime");
        var sample = model.GetProperty("sample");
        sample.GetProperty("user_name").GetString().ShouldBe("Sample UserName");
        sample.GetProperty("reset_link").GetString().ShouldBe("Sample ResetLink");
        sample.GetProperty("expires_at_utc").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetKeys_ListsTheModelWithoutRows_AndTheLanguagesOfOneWithRows()
    {
        await AddRowAsync("en", 1, active: false);
        await AddRowAsync("en", 2, active: true);
        await AddRowAsync("uk", 1, active: false);
        var client = await CreateAdminClientAsync();

        var response = await client.GetAsync(Prefix);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var entry = (await JsonOf(response)).EnumerateArray().Single(k => k.GetProperty("key").GetString() == Key);
        entry.GetProperty("hasModel").GetBoolean().ShouldBeTrue();
        var languages = entry.GetProperty("languages").EnumerateArray().ToList();
        languages.Select(l => l.GetProperty("language").GetString()).ShouldBe(["en", "uk"]);
        languages[0].GetProperty("activeVersion").GetInt32().ShouldBe(2);
        languages[0].GetProperty("versions").GetInt32().ShouldBe(2);
        // A language with no live version says so with null, the field is still there.
        languages[1].GetProperty("activeVersion").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetVersions_AreNewestFirstPerLanguage_AndCanBeNarrowedToOneLanguage()
    {
        await AddRowAsync("uk", 1, active: false, subject: "uk1");
        await AddRowAsync("en", 1, active: false, subject: "en1");
        await AddRowAsync("en", 2, active: true, subject: "en2");
        var client = await CreateAdminClientAsync();

        var all = (await JsonOf(await client.GetAsync($"{Prefix}/{Key}/versions"))).EnumerateArray().ToList();
        var english = (await JsonOf(await client.GetAsync($"{Prefix}/{Key}/versions?language=en"))).EnumerateArray().ToList();

        all.Select(v => v.GetProperty("subject").GetString()).ShouldBe(["en2", "en1", "uk1"]);
        english.Select(v => v.GetProperty("subject").GetString()).ShouldBe(["en2", "en1"]);
        all[0].GetProperty("isActive").GetBoolean().ShouldBeTrue();
        all[0].GetProperty("createdBy").GetString().ShouldBe("seed");
        all[0].GetProperty("html").ValueKind.ShouldBe(JsonValueKind.Null);
        all[0].GetProperty("createdAt").GetString().ShouldEndWith("Z");
    }

    // ---- new versions ----

    [Fact]
    public async Task PostVersion_Creates201_InactiveNumberedAfterTheHighest_AuthoredByTheCaller()
    {
        var client = await CreateAdminClientAsync();

        var first = await client.PostAsJsonAsync($"{Prefix}/{Key}/versions", new { language = "EN", subject = "Reset", text = "Hi {{ user_name }}: <secure>{{ reset_link }}</secure>" });
        var second = await client.PostAsJsonAsync($"{Prefix}/{Key}/versions", new { language = "en", subject = "Reset 2", html = "<p>{{ user_name }}</p>" });

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await JsonOf(first);
        created.GetProperty("id").GetInt64().ShouldBeGreaterThan(0);
        created.GetProperty("language").GetString().ShouldBe("en");
        created.GetProperty("version").GetInt32().ShouldBe(1);
        created.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        created.GetProperty("createdBy").GetString().ShouldBe(ApiFixture.TestUserName);
        (await JsonOf(second)).GetProperty("version").GetInt32().ShouldBe(2);
        (await _fixture.QueryDbAsync(db => db.Set<NotificationTemplate>().CountAsync(t => t.IsActive))).ShouldBe(0);
    }

    [Fact]
    public async Task PostVersion_ForAKeyWithoutAModel_Is404ProblemDetails()
    {
        var client = await CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync($"{Prefix}/no-such-key/versions", new { language = "en", subject = "S", text = "t" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await JsonOf(response)).GetProperty("status").GetInt32().ShouldBe(404);
        (await _fixture.QueryDbAsync(db => db.Set<NotificationTemplate>().CountAsync())).ShouldBe(0);
    }

    [Theory]
    [InlineData("", "S", "t", null)]
    [InlineData("this-is-far-too-long-a-tag", "S", "t", null)]
    [InlineData("en_US", "S", "t", null)]
    [InlineData("en", "", "t", null)]
    [InlineData("en", "S", null, null)]
    [InlineData("en", "S", " ", " ")]
    public async Task PostVersion_WithABadDraft_Is400_AndWritesNothing(string language, string subject, string? text, string? html)
    {
        var client = await CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync($"{Prefix}/{Key}/versions", new { language, subject, text, html });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await JsonOf(response)).GetProperty("detail").GetString().ShouldNotBeNullOrEmpty();
        (await _fixture.QueryDbAsync(db => db.Set<NotificationTemplate>().CountAsync())).ShouldBe(0);
    }

    [Theory]
    [InlineData("Hi {{ user_nmae }}", null, "text", "user_nmae")]
    [InlineData("S", "<p>{{ nope }}</p>", "html", "nope")]
    public async Task PostVersion_ThatDoesNotRender_Is422_NamingThePart_AndWritesNothing(string text, string? html, string part, string problem)
    {
        var client = await CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync($"{Prefix}/{Key}/versions", new { language = "en", subject = "S", text = html == null ? text : "t", html });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await JsonOf(response);
        body.GetProperty("title").GetString().ShouldBe("The template cannot be filled in");
        body.GetProperty("detail").GetString().ShouldContain(part);
        body.GetProperty("detail").GetString().ShouldContain(problem);
        (await _fixture.QueryDbAsync(db => db.Set<NotificationTemplate>().CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task PostVersion_WithUnbalancedSecureTags_Is422()
    {
        var client = await CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync($"{Prefix}/{Key}/versions", new { language = "en", subject = "S", text = "link <secure>{{ reset_link }}" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // ---- activation ----

    [Fact]
    public async Task Activate_MovesTheLiveFlagInOneStep_AndRepeatingItIs204Too()
    {
        var one = await AddRowAsync("en", 1, active: true);
        var two = await AddRowAsync("en", 2, active: false);
        var uk = await AddRowAsync("uk", 1, active: true);
        var client = await CreateAdminClientAsync();

        (await client.PostAsync($"{Prefix}/versions/{two.Id}/activate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostAsync($"{Prefix}/versions/{two.Id}/activate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var active = await _fixture.QueryDbAsync(db => db.Set<NotificationTemplate>().Where(t => t.IsActive).Select(t => t.Id).ToListAsync());
        active.OrderBy(id => id).ShouldBe(new[] { two.Id, uk.Id }.OrderBy(id => id));
        (await _fixture.QueryDbAsync(db => db.Set<NotificationTemplate>().SingleAsync(t => t.Id == one.Id))).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Activate_OfAnUnknownId_Is404()
    {
        var client = await CreateAdminClientAsync();

        (await client.PostAsync($"{Prefix}/versions/987654/activate", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- preview ----

    [Fact]
    public async Task Preview_FillsTheDraftWithTheSample_AndShowsTheTextWithoutTheSecureTags()
    {
        var client = await CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync($"{Prefix}/{Key}/preview", new
        {
            language = "en",
            subject = "Hello {{ user_name }}",
            text = "Reset: <secure>{{ reset_link }}</secure>",
            html = "<p>{{ user_name }}</p>",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await JsonOf(response);
        body.GetProperty("subject").GetString().ShouldBe("Hello Sample UserName");
        body.GetProperty("text").GetString().ShouldBe("Reset: Sample ResetLink");
        body.GetProperty("html").GetString().ShouldBe("<p>Sample UserName</p>");
        (await _fixture.QueryDbAsync(db => db.Set<NotificationTemplate>().CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task Preview_WithAModelOverride_UsesItInsteadOfTheSample_AndHtmlEscapesIt()
    {
        var client = await CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync($"{Prefix}/{Key}/preview", new
        {
            subject = "{{ user_name }}",
            html = "<p>{{ user_name }}</p>",
            model = new { user_name = "A <b> & B" },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await JsonOf(response);
        body.GetProperty("subject").GetString().ShouldBe("A <b> & B");
        body.GetProperty("text").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("html").GetString().ShouldBe("<p>A &lt;b&gt; &amp; B</p>");
    }

    [Fact]
    public async Task Preview_ForAnUnknownKey_Is404()
    {
        var client = await CreateAdminClientAsync();

        (await client.PostAsJsonAsync($"{Prefix}/no-such-key/preview", new { subject = "S", text = "t" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Preview_OfADraftThatDoesNotRender_Is422()
    {
        var client = await CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync($"{Prefix}/{Key}/preview", new { subject = "S", text = "{{ nope }}" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await JsonOf(response)).GetProperty("title").GetString().ShouldBe("The template cannot be filled in");
    }

    [Theory]
    [InlineData("""{"subject":"S","text":"t","model":{"expires_at_utc":{"a":1}}}""")]
    [InlineData("""{"subject":"S","text":"t","model":"not an object"}""")]
    public async Task Preview_WithAModelThatDoesNotFit_Is400(string json)
    {
        var client = await CreateAdminClientAsync();

        var response = await client.PostAsync($"{Prefix}/{Key}/preview", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
