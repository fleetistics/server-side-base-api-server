using api_server.Auth;
using api_server.Controllers.Log;
using api_server.Controllers.MapData.Services;
using api_server.Medias;
using api_server.Controllers.Teams;
using api_server.Controllers.Teams.Services;
using api_server.Controllers.Translations;
using api_server.Users;
using api_server.Idempotency;
using api_server.Service;
using api_server.Services.Feeders;
using exs.dbContextCommons;
using mf.aiApi.mainDatabase.DatabaseContext;
using exs.notifications_service;
using exs.notifications_service.Sender;

namespace api_server.core
{
	public static class DiHelper
	{
		public static void RegisterServices(IServiceCollection services, IConfiguration configuration)
		{
			services
				.AddDbServices<MainDatabaseContext>(configuration)
				.AddNotificationSender(configuration)
				// Only used when NotificationService:FcmTransport is "Sender"; user_session is where this host keeps devices.
				.AddUserSessionFcmHooks()
				// Only used when NotificationService:EmailTransport is "Sender"; the address is the one on the user record.
				.AddEmailDestinationResolver<api_server.Services.Notifications.UserEmailDestinationResolver>()
				.Configure<MediaSettings>(configuration.GetSection("Media"))
				.Configure<ClientLogRetentionOptions>(configuration.GetSection("ClientLog"))
				.Configure<AuthOptions>(configuration.GetSection("Auth"))
				.Configure<api_server.Controllers.Fleet.Services.FuelEconomyOptions>(configuration.GetSection("FuelEconomy"))
				.AddScoped<api_server.Controllers.Fleet.Services.IFuelEconomyService, api_server.Controllers.Fleet.Services.FuelEconomyService>()
				.AddSingleton<PasswordHasher>()
				.AddSingleton(TimeProvider.System)
				.AddSingleton<LoginThrottle>()
				.AddScoped<IPasswordResetKeySender, NoDeliveryPasswordResetKeySender>()
				.AddScoped<AuthService>()
				.AddScoped<IMediaStorage, MediaStorageService>()
				.AddScoped<IMediaInboundProcessor, MediaInboundProcessor>()
				.AddScoped<IUserService, UserService>()
				.AddScoped<IUserLocationPrivacyService, UserLocationPrivacyService>()
				.AddScoped<IUserSettingsService, UserSettingsService>()
				.AddScoped<IUserEmergencyAlertService, UserEmergencyAlertService>()
				.AddScoped<IUserCurrentContextService, UserCurrentContextService>()
				.AddScoped<ITeamService, TeamService>()
				.AddScoped<ITeamContextService, TeamContextService>()
				.AddScoped<ITranslationService, TranslationService>()
				.AddSingleton<MediaUrlResolver>()
				.AddSingleton<MediaPreviewService>()
				.AddSingleton<IMediaPreviewQueue>(sp => sp.GetRequiredService<MediaPreviewService>())
				.AddHostedService(sp => sp.GetRequiredService<MediaPreviewService>())
				.AddHostedService<ClientLogRetentionService>()
				.AddSingleton<MobileGpsDeviceMapTrackService>()
                .AddSingleton<QrCodeBuilder>()
                .AddHostedService(sp => sp.GetRequiredService<MobileGpsDeviceMapTrackService>())
				.Configure<MockTeamMemberMoveOptions>(configuration.GetSection("MockTeamMembers"))
				.AddSingleton<MockTeamMemberMove>()
				.AddSingleton<IMockTeamMemberMove>(sp => sp.GetRequiredService<MockTeamMemberMove>())
				.AddHostedService(sp => sp.GetRequiredService<MockTeamMemberMove>())
				// Tells the feeders which company's map is being watched (best effort; off unless Feeders:Heartbeat:BaseUrl is set).
				.AddFeederHeartbeat(configuration)
				.AddMemoryCache()
				.AddSingleton<IdempotencyStore>()
				;
		}
	}
}
