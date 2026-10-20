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
using exs.dbContextCommons;
using mf.aiApi.mainDatabase.DatabaseContext;
using exs.notifications_service;

namespace api_server.core
{
	public static class DiHelper
	{
		public static void RegisterServices(IServiceCollection services, IConfiguration configuration)
		{
			services
				.AddDbServices<MainDatabaseContext>(configuration)
				.AddNotificationSender()
				.Configure<MediaSettings>(configuration.GetSection("Media"))
				.Configure<ClientLogRetentionOptions>(configuration.GetSection("ClientLog"))
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
				.AddAutoMapper(cfg => cfg.AddProfile<TeamMappingProfile>())
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
				.AddMemoryCache()
				.AddSingleton<IdempotencyStore>()
				;
		}
	}
}
