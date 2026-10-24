namespace api_server.IntegrationTests;

/// <summary>
/// The same real host and real PostgreSQL as <see cref="ApiFixture"/>, with
/// NotificationService:FcmTransport = "Sender" pointing at a <see cref="FakeFcmSenderApp"/> on a local port.
/// </summary>
public sealed class FcmModeApiFixture : ApiFixture
{
    public const string AccountedAppUid = "app-a";
    public const string AccountForThatApp = "account-a";

    public FakeFcmSenderApp Sender { get; } = new();

    protected override void ConfigureNotificationEnvironment()
    {
        Environment.SetEnvironmentVariable("NotificationService__FcmTransport", "Sender");
        Environment.SetEnvironmentVariable("NotificationService__FcmSender__BaseUrl", Sender.BaseUrl);
        Environment.SetEnvironmentVariable("NotificationService__FcmSender__ApiKey", FakeFcmSenderApp.ApiKey);
        // The defaults (5 s and 3 s) are fallbacks for a quiet system; the tests should not wait on them.
        Environment.SetEnvironmentVariable("NotificationService__FcmSender__RelayPollIntervalSeconds", "1");
        Environment.SetEnvironmentVariable("NotificationService__FcmSender__ResultPollIntervalSeconds", "1");
        Environment.SetEnvironmentVariable($"NotificationService__FcmSender__AccountsByAppId__{AccountedAppUid}", AccountForThatApp);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Sender.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class FcmModeApiCollection : ICollectionFixture<FcmModeApiFixture>
{
    public const string Name = "api-fcm-mode";
}
