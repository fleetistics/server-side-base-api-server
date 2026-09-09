using api_server.Controllers.Users.Dto;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Users.Services
{
	public interface IUserLocationPrivacyService
	{
		/// <summary>Null when the user has no location privacy record.</summary>
		Task<UserLocationPrivacyDto?> GetUserLocationPrivacyAsync(int userId, CancellationToken cancellationToken);

		/// <summary>Applies only the fields present in <paramref name="patch"/>. Null when the user has no location privacy record.</summary>
		Task<UserLocationPrivacyDto?> PatchUserLocationPrivacyAsync(int userId, UserLocationPrivacyPatchDto patch, CancellationToken cancellationToken);
	}

	public sealed class UserLocationPrivacyService : IUserLocationPrivacyService
	{
		public UserLocationPrivacyService(IRepository repository, ILogger<UserLocationPrivacyService> logger)
		{
			mRepository = repository;
			mLogger = logger;
		}

		public async Task<UserLocationPrivacyDto?> GetUserLocationPrivacyAsync(int userId, CancellationToken cancellationToken)
		{
			var entity = await mRepository.GetQueryable<UserLocationPrivacy>(e => e.UserId == userId).Select(e => new UserLocationPrivacyDto(e)).FirstOrDefaultAsync(cancellationToken);
			if ( entity == null) entity = new UserLocationPrivacyDto()
				{
					PrivacyMode = 0,
					LatestUpdate = DateTime.UtcNow
				};

            return entity;
		}

		public async Task<UserLocationPrivacyDto?> PatchUserLocationPrivacyAsync(int userId, UserLocationPrivacyPatchDto patch, CancellationToken cancellationToken)
		{
			var entity = await mRepository.GetQueryable<UserLocationPrivacy>(e => e.UserId == userId)
				.FirstOrDefaultAsync(cancellationToken);
            mLogger.LogInformation($"PatchUserLocationPrivacyAsync: userId={userId}, patch.PrivacyMode={patch.PrivacyMode} {entity}");

			if (entity == null)
			{
				entity = new UserLocationPrivacy()
				{
					UserId = userId,
					PrivacyMode = (patch.PrivacyMode.IsSet ? patch.PrivacyMode.Value : (short)0)
				};
                mRepository.Create(entity);

                mLogger.LogInformation($"PatchUserLocationPrivacyAsync: userId={userId} NEW");
			}
			else
			{
                mLogger.LogInformation($"PatchUserLocationPrivacyAsync: userId={userId} existing entity.PrivacyMode {entity.PrivacyMode}");
                if (patch.PrivacyMode.IsSet) entity.PrivacyMode = patch.PrivacyMode.Value;
			}

			await mRepository.SaveAsync(cancellationToken);
			return new UserLocationPrivacyDto(entity);
		}

		private readonly IRepository mRepository;
        private readonly ILogger mLogger;
    }
}
