using api_server.Controllers.Users.Dto;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Users.Services
{
	public interface IUserSettingsService
	{
		Task<List<UserSettingsDto>> GetUserSettingsAsync(int userId, short clientApplicationId, short clientDevicePlatformId, CancellationToken cancellationToken);

		/// <summary>Creates or updates the setting identified by <paramref name="name"/> (scoped to the given client application/device platform) for the user.</summary>
		Task<UserSettingsDto> PutUserSettingAsync(int userId, string name, string? value, short? clientApplicationId, short? clientDevicePlatformId, CancellationToken cancellationToken);
	}

	public sealed class UserSettingsService : IUserSettingsService
	{
		public UserSettingsService(IRepository repository)
		{
			mRepository = repository;
		}

		public async Task<List<UserSettingsDto>> GetUserSettingsAsync(int userId, short clientApplicationId, short clientDevicePlatformId, CancellationToken cancellationToken)
		{
            var res = new List<UserSettingsDto>();
            var settings = await mRepository.GetQueryable<UserSettings>(e => (e.UserId == null || e.UserId == userId)
                    && (e.ClientApplicationId == null || e.ClientApplicationId == clientApplicationId)
                    && (e.ClientDevicePlatformId == null || e.ClientDevicePlatformId == clientDevicePlatformId))
				.ToListAsync(cancellationToken);

			foreach (var (name, grp_lst) in settings.GroupBy(e => e.Name).Select(g => (g.Key, g.ToList())))
			{
				// A null Value at a tier marks it reset — fall through to the next broader tier instead of masking it.
				var best = grp_lst.FirstOrDefault(e => e.Value != null && e.UserId == userId && e.ClientApplicationId == clientApplicationId && e.ClientDevicePlatformId == clientDevicePlatformId)
					?? grp_lst.FirstOrDefault(e => e.Value != null && e.UserId == userId && e.ClientApplicationId == clientApplicationId && e.ClientDevicePlatformId == null)
					?? grp_lst.FirstOrDefault(e => e.Value != null && e.UserId == userId && e.ClientApplicationId == null && e.ClientDevicePlatformId == null)
					?? grp_lst.FirstOrDefault(e => e.Value != null && e.UserId == null && e.ClientApplicationId == null && e.ClientDevicePlatformId == null);

				if (best != null) res.Add(new UserSettingsDto(best));
			}

			return res;
        }

		public async Task<UserSettingsDto> PutUserSettingAsync(int userId, string name, string? value, short? clientApplicationId, short? clientDevicePlatformId, CancellationToken cancellationToken)
		{
			var entity = await mRepository.GetQueryable<UserSettings>(e => e.UserId == userId
					&& e.Name == name
					&& e.ClientApplicationId == clientApplicationId
					&& e.ClientDevicePlatformId == clientDevicePlatformId)
				.FirstOrDefaultAsync(cancellationToken);

			if (entity == null)
			{
				entity = new UserSettings()
				{
					UserId = userId,
					Name = name,
					Value = value,
					ClientApplicationId = clientApplicationId,
					ClientDevicePlatformId = clientDevicePlatformId
				};
				mRepository.Create(entity);
			}
			else
			{
				entity.Value = value;
			}

			// Setting a broader-scoped value makes any narrower overrides beneath it stale, so drop them.
			if (clientApplicationId != null && clientDevicePlatformId == null)
			{
				mRepository.DeleteAll<UserSettings>(e => e.UserId == userId && e.Name == name
					&& e.ClientApplicationId == clientApplicationId && e.ClientDevicePlatformId != null);
			}
			else if (clientApplicationId == null && clientDevicePlatformId == null)
			{
				mRepository.DeleteAll<UserSettings>(e => e.UserId == userId && e.Name == name
					&& e.ClientApplicationId != null);
			}

			await mRepository.SaveAsync(cancellationToken);
			return new UserSettingsDto(entity);
		}

		private readonly IRepository mRepository;
	}
}
