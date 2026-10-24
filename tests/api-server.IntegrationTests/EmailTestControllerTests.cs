using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using exs.notifications_model.Outbox;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// /api/test/email (EmailTestController), the opt-in endpoints for trying the real email sender from a dev machine.
/// With the flag on and the email transport on, against the fake sender app: a mail goes out and its state can be
/// polled without the address ever coming back.
/// </summary>
[Collection(EmailModeApiCollection.Name)]
public sealed class EmailTestControllerTests : IAsyncLifetime
{
    private readonly EmailModeApiFixture _fixture;

    public EmailTestControllerTests(EmailModeApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync()
    {
        _fixture.Sender.Reset();
        return new(_fixture.ResetDatabaseAsync());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Post_SendsTheMailToTheSender_Returns202WithAReference_AndPollingEndsInSentWithoutTheAddress()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/test/email", new
        {
            to = "tess.tester@example.com",
            toName = "Tess Tester",
            subject = "Hello from the test",
            text = "Body of the test mail",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var reference = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetString();
        reference.ShouldNotBeNullOrEmpty();

        // What the email sender was asked to deliver.
        await NotificationRaiser.WaitUntilAsync(() => Task.FromResult(_fixture.Sender.Messages.Count == 1));
        var mail = _fixture.Sender.Messages.ShouldHaveSingleItem();
        mail.Channel.ShouldBe("email");
        mail.Ref.ShouldBe(reference);
        mail.Body.To.Address.ShouldBe("tess.tester@example.com");
        mail.Body.To.Name.ShouldBe("Tess Tester");
        mail.Body.Subject.ShouldBe("Hello from the test");
        mail.Body.Text.ShouldBe("Body of the test mail");

        // Polling is only a read; it ends in completed / sent, and no body of any poll holds the address or the text.
        string json;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (true)
        {
            var poll = await client.GetAsync($"/api/test/email/{reference}");
            poll.StatusCode.ShouldBe(HttpStatusCode.OK);
            json = await poll.Content.ReadAsStringAsync();
            assertNoSecrets(json);
            if (JsonDocument.Parse(json).RootElement.GetProperty("state").GetString() == "completed") break;
            (DateTime.UtcNow < deadline).ShouldBeTrue($"the email never reached state 'completed'; last answer: {json}");
            await Task.Delay(100);
        }

        var status = JsonDocument.Parse(json).RootElement;
        status.GetProperty("reference").GetString().ShouldBe(reference);
        status.GetProperty("resultStatus").GetString().ShouldBe("sent");
        status.GetProperty("providerMessageId").GetString().ShouldNotBeNullOrEmpty();
        status.GetProperty("destinationInvalid").GetBoolean().ShouldBeFalse();
        status.GetProperty("attempts").GetInt32().ShouldBe(1);
        status.GetProperty("messageId").GetGuid().ShouldNotBe(Guid.Empty);
        status.GetProperty("completedAt").ValueKind.ShouldBe(JsonValueKind.String);
        status.TryGetProperty("createdAt", out _).ShouldBeTrue();
        status.TryGetProperty("envelope", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Post_WithOnlyAnAddress_UsesTheDefaultSubjectAndText()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/test/email", new { to = "tess.tester@example.com" });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await NotificationRaiser.WaitUntilAsync(() => Task.FromResult(_fixture.Sender.Messages.Count == 1));
        var mail = _fixture.Sender.Messages.ShouldHaveSingleItem();
        mail.Body.Subject.ShouldStartWith("Test email 20");
        mail.Body.Subject.ShouldEndWith(" UTC");
        mail.Body.Text.ShouldBe("This is a test email sent from base-api-server through the email sender.");
        mail.Body.Html.ShouldBeNull();
    }

    [Fact]
    public async Task Post_WithoutALogin_Is401_AndWritesNothing()
    {
        var anonymous = _fixture.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/test/email", new { to = "tess.tester@example.com" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().CountAsync())).ShouldBe(0);
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("Sam <sam@example.com>")]
    public async Task Post_WithABadAddress_Is400ProblemDetails_AndWritesNoOutboxRow(string address)
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/test/email", new { to = address });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(address);
        var problem = JsonDocument.Parse(body).RootElement;
        problem.GetProperty("status").GetInt32().ShouldBe(400);
        problem.GetProperty("detail").GetString().ShouldNotBeNullOrEmpty();
        (await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().CountAsync())).ShouldBe(0);
        _fixture.Sender.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_WithoutAnAddress_Is400_AndWritesNoOutboxRow()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/test/email", new { subject = "No recipient" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task Get_OfAnUnknownReference_Is404()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        (await client.GetAsync("/api/test/email/does-not-exist")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ShowsAFailureWithoutTheAddress_EvenWhenTheMailServerQuotedIt()
    {
        await _fixture.QueryDbAsync(async db =>
        {
            db.Set<OutboxMessage>().Add(new OutboxMessage
            {
                MessageId = Guid.NewGuid(),
                Channel = "email",
                Ref = "failed-one",
                Account = "alias-a",
                // What the relay clears once the sender has accepted a message; here it still holds an address and a body.
                Envelope = """{"body":{"to":{"address":"leak@example.com"},"text":"secret body"}}""",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                State = OutboxState.Completed,
                ResultStatus = "failed",
                ErrorKind = "destination_invalid",
                ErrorCode = "550 5.1.1",
                ErrorMessage = "550 5.1.1 <leak@example.com>: Recipient address rejected: leak@example.com does not exist",
                DestinationInvalid = true,
                Attempts = 1,
                CompletedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return 0;
        });
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/test/email/failed-one");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.ShouldNotContain("leak@example.com");
        json.ShouldNotContain("secret body");
        var status = JsonDocument.Parse(json).RootElement;
        status.GetProperty("state").GetString().ShouldBe("completed");
        status.GetProperty("resultStatus").GetString().ShouldBe("failed");
        status.GetProperty("errorKind").GetString().ShouldBe("destination_invalid");
        status.GetProperty("errorCode").GetString().ShouldBe("550 5.1.1");
        status.GetProperty("errorMessage").GetString().ShouldContain("Recipient address rejected");
        status.GetProperty("destinationInvalid").GetBoolean().ShouldBeTrue();
        status.GetProperty("account").GetString().ShouldBe("alias-a");
    }

    private static void assertNoSecrets(string json)
    {
        json.ShouldNotContain("tess.tester@example.com", Case.Insensitive);
        json.ShouldNotContain("Tess Tester");
        json.ShouldNotContain("Body of the test mail");
    }
}

/// <summary>The host with the email transport on, but TestEmail:Enabled unset: the endpoints must not exist for anyone.</summary>
public sealed class EmailModeTestEmailOffApiFixture : EmailModeApiFixture
{
    protected override string? TestEmailEnabled => null;
}

[CollectionDefinition(Name)]
public sealed class EmailModeTestEmailOffApiCollection : ICollectionFixture<EmailModeTestEmailOffApiFixture>
{
    public const string Name = "api-email-mode-test-email-off";
}

/// <summary>
/// The flag is off (the default): both endpoints answer 404 even to a logged-in user, even though the email transport
/// is on and a message with that reference exists, and POST sends and writes nothing.
/// </summary>
[Collection(EmailModeTestEmailOffApiCollection.Name)]
public sealed class EmailTestControllerFlagOffTests : IAsyncLifetime
{
    private readonly EmailModeTestEmailOffApiFixture _fixture;

    public EmailTestControllerFlagOffTests(EmailModeTestEmailOffApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync()
    {
        _fixture.Sender.Reset();
        return new(_fixture.ResetDatabaseAsync());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task BothEndpoints_Are404_AndNothingIsWritten()
    {
        await _fixture.QueryDbAsync(async db =>
        {
            db.Set<OutboxMessage>().Add(new OutboxMessage
            {
                MessageId = Guid.NewGuid(),
                Channel = "email",
                Ref = "existing-ref",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                State = OutboxState.Completed,
                ResultStatus = "sent",
            });
            await db.SaveChangesAsync();
            return 0;
        });
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var post = await client.PostAsJsonAsync("/api/test/email", new { to = "tess.tester@example.com" });
        var getKnown = await client.GetAsync("/api/test/email/existing-ref");
        var getUnknown = await client.GetAsync("/api/test/email/nothing");

        post.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        getKnown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        getUnknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await getKnown.Content.ReadAsStringAsync()).ShouldNotContain("completed");

        // Only the row this test inserted; the POST wrote none, and the sender never heard of anything.
        (await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().CountAsync())).ShouldBe(1);
        await Task.Delay(1500);
        _fixture.Sender.Messages.ShouldBeEmpty();
    }
}

/// <summary>The flag is on but the email transport is not: the legacy host with only the test endpoints opted in.</summary>
public sealed class TestEmailOnWithoutEmailTransportApiFixture : ApiFixture
{
    protected override void ConfigureNotificationEnvironment()
    {
        Environment.SetEnvironmentVariable("TestEmail__Enabled", "true");
    }
}

[CollectionDefinition(Name)]
public sealed class TestEmailOnWithoutEmailTransportApiCollection : ICollectionFixture<TestEmailOnWithoutEmailTransportApiFixture>
{
    public const string Name = "api-test-email-on-email-off";
}

/// <summary>The flag is on but NotificationService:EmailTransport is not "Sender": POST says so (409) and writes nothing.</summary>
[Collection(TestEmailOnWithoutEmailTransportApiCollection.Name)]
public sealed class EmailTestControllerTransportOffTests : IAsyncLifetime
{
    private readonly TestEmailOnWithoutEmailTransportApiFixture _fixture;

    public EmailTestControllerTransportOffTests(TestEmailOnWithoutEmailTransportApiFixture fixture) => _fixture = fixture;

    public ValueTask InitializeAsync() => new(_fixture.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Post_WhenTheEmailTransportIsOff_Is409ProblemDetails_AndWritesNothing()
    {
        var client = await _fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/test/email", new { to = "tess.tester@example.com" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(body).RootElement.GetProperty("status").GetInt32().ShouldBe(409);
        body.ShouldContain("NotificationService:EmailTransport");
        body.ShouldNotContain("tess.tester@example.com");
        (await _fixture.QueryDbAsync(db => db.Set<OutboxMessage>().CountAsync())).ShouldBe(0);
    }
}
