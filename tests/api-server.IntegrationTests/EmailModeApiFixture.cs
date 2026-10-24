namespace api_server.IntegrationTests;

/// <summary>
/// The same real host and real PostgreSQL as <see cref="ApiFixture"/>, with
/// NotificationService:EmailTransport = "Sender" pointing at a <see cref="FakeEmailSenderApp"/> on a local
/// port. Email is chosen per call (CreateEmailNotification, CreateEmail), not by type; push stays on the legacy queue.
/// </summary>
public class EmailModeApiFixture : ApiFixture
{
    /// <summary>The value of TestEmail:Enabled (the opt-in /api/test/email endpoints); a variant fixture returns null (unset, so the default, off).</summary>
    protected virtual string? TestEmailEnabled => "true";

    /// <summary>The notification type id the email tests use; any id would do, since the channel is the call, not the type.</summary>
    public const short EmailedType = 5;

    public FakeEmailSenderApp Sender { get; } = new();

    protected override void ConfigureNotificationEnvironment()
    {
        Environment.SetEnvironmentVariable("NotificationService__EmailTransport", "Sender");
        Environment.SetEnvironmentVariable("NotificationService__EmailSender__BaseUrl", Sender.BaseUrl);
        Environment.SetEnvironmentVariable("NotificationService__EmailSender__ApiKey", FakeEmailSenderApp.ApiKey);
        Environment.SetEnvironmentVariable("NotificationService__EmailSender__RelayPollIntervalSeconds", "1");
        Environment.SetEnvironmentVariable("NotificationService__EmailSender__ResultPollIntervalSeconds", "1");
        Environment.SetEnvironmentVariable("TestEmail__Enabled", TestEmailEnabled);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Sender.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class EmailModeApiCollection : ICollectionFixture<EmailModeApiFixture>
{
    public const string Name = "api-email-mode";
}
