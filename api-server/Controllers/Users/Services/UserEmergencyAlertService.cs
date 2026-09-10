using api_server.Controllers.Users.Dto;
using db_model.UserManagement;
using exs.Commons;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Users.Services
{
	public interface IUserEmergencyAlertService
	{
		Task<UserEmergencyAlertDto> CreateMyEmergencyAlertAsync(int userId, CreateUserEmergencyAlertDto body, CancellationToken cancellationToken);

		/// <summary>Null when the user has no active (uncompleted) emergency alert.</summary>
		Task<MyUserEmergencyAlertDto?> GetMyActiveEmergencyAlertAsync(int userId, CancellationToken cancellationToken);

		/// <summary>Completes the user's active (uncompleted) emergency alert. Null when there is none.</summary>
		Task<UserEmergencyAlertDto?> CompleteMyEmergencyAlertAsync(int userId, CompleteUserEmergencyAlertDto body, CancellationToken cancellationToken);
	}

	public sealed class UserEmergencyAlertService : IUserEmergencyAlertService
	{
		public UserEmergencyAlertService(IRepository repository)
		{
			mRepository = repository;
		}

		public async Task<UserEmergencyAlertDto> CreateMyEmergencyAlertAsync(int userId, CreateUserEmergencyAlertDto body, CancellationToken cancellationToken)
		{
            var entity = await activeAlertQuery(userId).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
			if (entity != null) throw new TUserMessageException("An active alert already exists.");
            entity = new UserEmergencyAlert
			{
				UserId = userId,
				CreatedDate = DateTime.UtcNow,
				Latitude = body.Latitude,
				Longitude = body.Longitude,
				StatusId = UserEmergencyAlertStatus.Active
			};
			mRepository.Create(entity);
			await mRepository.SaveAsync(cancellationToken);

			return new UserEmergencyAlertDto(entity);
		}

		public async Task<MyUserEmergencyAlertDto?> GetMyActiveEmergencyAlertAsync(int userId, CancellationToken cancellationToken)
		{
			var entity = await activeAlertQuery(userId).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
			return entity == null ? null : new MyUserEmergencyAlertDto(entity);
		}

		public async Task<UserEmergencyAlertDto?> CompleteMyEmergencyAlertAsync(int userId, CompleteUserEmergencyAlertDto body, CancellationToken cancellationToken)
		{
			var entity = await activeAlertQuery(userId).FirstOrDefaultAsync(cancellationToken);
			if (entity == null) return null;

			entity.CompletedDate = DateTime.UtcNow;
			entity.CompleteLatitude = body.CompleteLatitude;
			entity.CompleteLongitude = body.CompleteLongitude;
			entity.StatusId = UserEmergencyAlertStatus.ColosedByUser;

			await mRepository.SaveAsync(cancellationToken);
			return new UserEmergencyAlertDto(entity);
		}

		// The active alert is the most recent one this user hasn't closed yet - there's no
		// separate "current alert" pointer, so it's derived by query every time.
		private IQueryable<UserEmergencyAlert> activeAlertQuery(int userId) =>
			mRepository.GetQueryable<UserEmergencyAlert>(e => e.UserId == userId && e.StatusId == UserEmergencyAlertStatus.Active);

		private readonly IRepository mRepository;
	}
}
