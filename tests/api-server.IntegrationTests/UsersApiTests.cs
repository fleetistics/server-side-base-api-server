using System.Net;
using System.Net.Http.Json;
using db_model.UserManagement;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class UsersApiTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;

    public UsersApiTests(ApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetUsers_WithoutToken_Returns401()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/users");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUsers_ReturnsActiveUsers()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var users = await client.GetFromJsonAsync<List<UserResponse>>("/api/users");

        users.ShouldNotBeNull();
        users.ShouldContain(u => u.UserName == ApiFixture.TestUserName);
    }

    [Fact]
    public async Task GetUser_Unknown_Returns404()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/users/999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateUser_WithIdempotencyKey_CreatesUserRow()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var request = NewUserRequest();
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<UserResponse>();
        created!.Id.ShouldBeGreaterThan(0);

        var stored = await _fixture.QueryDbAsync(db =>
            db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == created.Id));
        stored.UserName.ShouldBe("new.user");
        stored.StatusId.ShouldBe(UserStatus.Active);
    }

    [Fact]
    public async Task CreateUser_SameIdempotencyKeyTwice_CreatesOnlyOneRow()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var key = Guid.NewGuid().ToString();

        var first = NewUserRequest();
        first.Headers.Add("Idempotency-Key", key);
        var firstResponse = await client.SendAsync(first);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<UserResponse>();

        var second = NewUserRequest();
        second.Headers.Add("Idempotency-Key", key);
        var secondResponse = await client.SendAsync(second);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<UserResponse>();

        secondBody!.Id.ShouldBe(firstBody!.Id); // cached response, not a new user

        var count = await _fixture.QueryDbAsync(db =>
            db.Set<User>().CountAsync(u => u.UserName == "new.user"));
        count.ShouldBe(1);
    }

    [Fact]
    public async Task CreateUser_WithoutIdempotencyKey_Returns400()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.SendAsync(NewUserRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateUser_IsNotSupported()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var userId = _fixture.TestUserId;

        var response = await client.PutAsJsonAsync($"/api/users/{userId}", new
        {
            Id = userId,
            UserName = "",
            DisplayName = "Renamed",
            FullName = "Renamed User",
            Phone = "8135550199",
            Email = "renamed@example.test",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task PatchUser_OmittedFields_AreLeftUnchanged()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var userId = _fixture.TestUserId;
        var before = await client.GetFromJsonAsync<UserResponse>($"/api/users/{userId}");

        var response = await client.PatchAsJsonAsync($"/api/users/{userId}", new { DisplayName = "Patched Name" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patched = await response.Content.ReadFromJsonAsync<UserResponse>();
        patched!.DisplayName.ShouldBe("Patched Name");
        // Fields never mentioned in the patch body must survive untouched.
        patched.Phone.ShouldBe(before!.Phone);
        patched.Email.ShouldBe(before.Email);
        patched.FullName.ShouldBe(before.FullName);
    }

    [Fact]
    public async Task PatchUser_ExplicitNull_ClearsNullableField()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var userId = _fixture.TestUserId;

        var response = await client.PatchAsJsonAsync($"/api/users/{userId}", new { Phone = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patched = await response.Content.ReadFromJsonAsync<UserResponse>();
        patched!.Phone.ShouldBeNull();

        var roundTrip = await client.GetFromJsonAsync<UserResponse>($"/api/users/{userId}");
        roundTrip!.Phone.ShouldBeNull();
    }

    [Fact]
    public async Task PatchUser_NullableValueType_RoundTripsThroughOptional()
    {
        // Distinct from the string-typed fields above: AvatarImageId is Optional<int?>,
        // exercising OptionalJsonConverterFactory against a nullable value type, not
        // just a reference type.
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var userId = _fixture.TestUserId;
        var mediaId = await _fixture.QueryDbAsync(async db =>
        {
            var media = new db_model.Media.UploadedMedia { FileName = "avatar.png", MediaType = 1 };
            db.Add(media);
            await db.SaveChangesAsync();
            return media.Id;
        });

        var setResponse = await client.PatchAsJsonAsync($"/api/users/{userId}", new { AvatarImageId = mediaId });
        setResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await setResponse.Content.ReadFromJsonAsync<UserResponse>())!.AvatarImageId.ShouldBe(mediaId);

        var clearResponse = await client.PatchAsJsonAsync($"/api/users/{userId}", new { AvatarImageId = (int?)null });
        clearResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await clearResponse.Content.ReadFromJsonAsync<UserResponse>())!.AvatarImageId.ShouldBeNull();
    }

    [Fact]
    public async Task PatchUser_ExplicitEmptyDisplayName_Returns400()
    {
        // DisplayName is DB-required — Optional<T> bypasses the implicit NRT-required
        // check UserDto gets for free on PUT, so UserPatchDto.Validate() must catch this.
        var client = await _fixture.CreateAuthenticatedClientAsync();
        var userId = _fixture.TestUserId;

        var response = await client.PatchAsJsonAsync($"/api/users/{userId}", new { DisplayName = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchUser_Unknown_Returns404()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.PatchAsJsonAsync("/api/users/999999", new { DisplayName = "X" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PatchUser_WithoutToken_Returns401()
    {
        var client = _fixture.CreateClient();

        var response = await client.PatchAsJsonAsync($"/api/users/{_fixture.TestUserId}", new { DisplayName = "X" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static HttpRequestMessage NewUserRequest() =>
        new(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new
            {
                UserName = "new.user",
                DisplayName = "New User",
                FullName = "Newly Created",
                Phone = "8135550111",
                Email = "new.user@example.test",
            }),
        };

    private sealed record UserResponse(
        int Id, string? UserName, string DisplayName, string? FullName, string? Phone, string? Email,
        int? AvatarImageId);
}
