using api_server.core;
using api_server.Medias;
using api_server.Service;
using api_server.Users;
using db_model.Companies;
using db_model.Missions;
using exs.commons.Utils;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace api_server.Missions
{
	public interface IMissionService
	{
		/// <summary>The mission, or null when it does not exist.</summary>
		Task<MissionDto?> GetMissionAsync(int missionId, CancellationToken cancellationToken);

		///// <summary>Id of the user's Active mission (as its creator), or null when they have none.</summary>
		//Task<int?> GetUserActiveMissionIdAsync(int userId, CancellationToken cancellationToken);

		/// <summary>The mission the user is still setting up (Creating status) with its details, or null when there is none.</summary>
		Task<CreatingMissionDto?> GetCreatingMissionByUserAsync(int userId, CancellationToken cancellationToken);

		/// <summary>Details of the mission, or null when there are none.</summary>
		Task<MissionDetailsDto?> GetMissionDetailsAsync(int missionId, CancellationToken cancellationToken);

		/// <summary>Primary target user of the mission, or null when there is none.</summary>
		Task<MissionPrimaryTargetUserDto?> GetPrimaryTargetUserAsync(int missionId, CancellationToken cancellationToken);

		/// <summary>
		/// Creates a mission in Creating status with its join key and QR code, applies the patch parts
		/// (mission, details, creator user). Returns the new mission id.
		/// </summary>
		Task<int> CreateMissionAsync(int userId,  MissionCreatePatchDto createMission, CancellationToken cancellationToken);

		/// <summary>Applies only the fields present in <paramref name="createMission"/> to a mission still in Creating status; only its creator can patch it.</summary>
		Task PatchCreateMissionAsync(int userId, int missionId, MissionCreatePatchDto createMission, CancellationToken cancellationToken);

		/// <summary>Sets the mission's single primary target user record (creates or replaces it); only the mission creator can do this.</summary>
		Task<MissionPrimaryTargetUserDto> AssignPrimaryTargetUserAsync(int userId, int missionId, int targetUserId, CancellationToken cancellationToken);

		/// <summary>Activates a Creating mission; only its creator can start it.</summary>
		Task StartMissionAsync(int userId, int missionId, CancellationToken cancellationToken);
	}

	public sealed class MissionService : IMissionService
	{
		public MissionService(QrCodeBuilder qrCodeBuilder, MediaUrlResolver mediaUrls, IMediaInboundProcessor mediaInboundProcessor, IRepository repository, IUserService users, ILogger<MissionService> logger)
		{
			mQrCodeBuilder = qrCodeBuilder;
			mMediaUrls = mediaUrls;
			mMediaInboundProcessor = mediaInboundProcessor;
			mRepository = repository;
			mUsers = users;
			mLogger = logger;
		}

		public async Task<MissionDto?> GetMissionAsync(int missionId, CancellationToken cancellationToken)
		{
			var mission = await mRepository.GetQueryable<Mission>(e => e.Id == missionId).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
			return ToDto(mission);
		}

		//public async Task<int?> GetUserActiveMissionIdAsync(int userId, CancellationToken cancellationToken)
		//{
		//	return await mRepository.GetQueryable<Mission>(e => e.CreatorUserId == userId && e.StatusId == MissionStatus.Active).Select(e => (int?)e.Id).FirstOrDefaultAsync(cancellationToken);
		//}

		public async Task<CreatingMissionDto?> GetCreatingMissionByUserAsync(int userId, CancellationToken cancellationToken)
		{
			var mission = await mRepository.GetQueryable<Mission>(e => e.CreatorUserId == userId && (e.StatusId == MissionStatus.Creating || e.StatusId == MissionStatus.GenerateKey)).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
			if (mission == null) return null;
			if (mission.StatusId == MissionStatus.GenerateKey)
			{
				// Half-created mission (no join key yet): discard it, as for teams.
				mRepository.Delete(mission);
				await mRepository.SaveAsync(cancellationToken);
				return null;
			}
			return new CreatingMissionDto
			{
				Mission = ToDto(mission)!,
				MissionDetails = await GetMissionDetailsAsync(mission.Id, cancellationToken)//,
				//PrimaryTargetUser = await GetPrimaryTargetUserAsync(mission.Id, cancellationToken)
			};
		}

		public async Task<MissionDetailsDto?> GetMissionDetailsAsync(int missionId, CancellationToken cancellationToken)
		{
			var details = await mRepository.GetQueryable<MissionDetails>(e => e.MissionId == missionId).Include(e => e.Medias).ThenInclude(e => e.Media).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
			if (details == null) return null;
			var dto = new MissionDetailsDto(details);
			foreach (var media in dto.Medias ?? Enumerable.Empty<MissionMediaDto>()) mMediaUrls.ApplyAbsoluteUrl(media.Media);
			return dto;
		}

		public async Task<MissionPrimaryTargetUserDto?> GetPrimaryTargetUserAsync(int missionId, CancellationToken cancellationToken)
		{
			return await mRepository.GetQueryable<MissionPrimaryTargetUser>(e => e.MissionId == missionId).AsNoTracking().Select(t => new MissionPrimaryTargetUserDto(t)).FirstOrDefaultAsync(cancellationToken);
		}

		public async Task<int> CreateMissionAsync(int userId, MissionCreatePatchDto createMission, CancellationToken cancellationToken)
		{
			if (createMission.Mission == null || !createMission.Mission.Name.IsSet) throw new ArgumentNullException(nameof(createMission.Mission));
			var entity = new Mission
			{
				CreatorUserId = userId,
				Name = createMission.Mission.Name.Value!,
				CreatedDate = DateTime.UtcNow,
				StatusId = MissionStatus.GenerateKey
			};
			mRepository.Create(entity);
			var attempts = 50;
			// Retry on JoinKey duplicates.
			while (true)
			{
				try
				{
					entity.JoinKey = SymbolKeyGenerator.GenerateSymbolKey(6);
					await mRepository.SaveAsync(cancellationToken);
					break;
				}
				// Only a duplicate key is worth another try; anything else (e.g. a NOT NULL violation) would fail every time.
				catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } && --attempts > 0)
				{
					mLogger.LogWarning(ex, $"Duplicate joinKey: {entity.JoinKey} attempts left {attempts}");
				}
			}
			entity.StatusId = MissionStatus.Creating;
			entity.JoinQrCode = await mQrCodeBuilder.CreateQrCodeFileAsync(entity.JoinKey, cancellationToken);

			var missionDetails = new MissionDetails { MissionId = entity.Id };
			if (createMission.MissionDetails != null)
			{
				missionDetails.ApplyPatch(createMission.MissionDetails);

				var insertedMedias = await mMediaInboundProcessor.ProcessAsync(createMission.MissionDetails, [], cancellationToken);
				foreach (var newMedia in insertedMedias)
				{
					mRepository.Create(new MissionUploadedMedia
					{
						MissionId = entity.Id,
						UploadedMediaId = newMedia.Id,
						GroupKey = createMission.MissionDetails.InsertMedias?.Find(e => e.Guid == newMedia.Guid)?.GroupKey
					});
				}
			}
			mRepository.Create(missionDetails);
			if (createMission.CreatorUser != null)
			{
				await mUsers.PatchUserAsync(userId, createMission.CreatorUser, cancellationToken);
			}
			await mRepository.SaveAsync(cancellationToken);
			return entity.Id;
		}

		public async Task PatchCreateMissionAsync(int userId, int missionId, MissionCreatePatchDto createMission, CancellationToken cancellationToken)
		{
			var mission = await mRepository.GetQueryable<Mission>(e => e.Id == missionId).SingleAsync(cancellationToken);
			if (mission.CreatorUserId != userId) throw new InvalidOperationException("Only the creator can change the mission");
			if (mission.StatusId != MissionStatus.Creating) throw new InvalidOperationException("Mission is not in creating status");
			if (createMission.Mission != null) mission.ApplyPatch(createMission.Mission);
			if (createMission.MissionDetails != null)
			{
				var missionDetails = await mRepository.GetQueryable<MissionDetails>(e => e.MissionId == missionId).Include(e => e.Medias).SingleAsync(cancellationToken);
				missionDetails.ApplyPatch(createMission.MissionDetails);
				var insertedMedias = await mMediaInboundProcessor.ProcessAsync(createMission.MissionDetails, missionDetails.Medias?.Select(m => m.UploadedMediaId).ToList() ?? [], cancellationToken);
				if (createMission.MissionDetails.RemoveMediaIds != null && missionDetails.Medias?.Count > 0)
				{
					foreach (var missionMedia in missionDetails.Medias)
					{
						if (createMission.MissionDetails.RemoveMediaIds.Contains(missionMedia.UploadedMediaId)) mRepository.Delete(missionMedia);
					}
				}
				foreach (var newMedia in insertedMedias)
				{
					mRepository.Create(new MissionUploadedMedia
					{
						MissionId = missionId,
						UploadedMediaId = newMedia.Id,
						GroupKey = createMission.MissionDetails.InsertMedias?.Find(e => e.Guid == newMedia.Guid)?.GroupKey
					});
				}
			}
			if (createMission.CreatorUser != null)
			{
				await mUsers.PatchUserAsync(userId, createMission.CreatorUser, cancellationToken);
			}
			await mRepository.SaveAsync(cancellationToken);
		}

		public async Task<MissionPrimaryTargetUserDto> AssignPrimaryTargetUserAsync(int userId, int missionId, int targetUserId, CancellationToken cancellationToken)
		{
			var mission = await mRepository.GetQueryable<Mission>(e => e.Id == missionId).AsNoTracking().SingleAsync(cancellationToken);
			if (mission.CreatorUserId != userId) throw new InvalidOperationException("Only the creator can assign the primary target user");

			var target = await mRepository.GetQueryable<MissionPrimaryTargetUser>(e => e.MissionId == missionId).FirstOrDefaultAsync(cancellationToken);
			if (target == null)
			{
				target = new MissionPrimaryTargetUser { MissionId = missionId };
				mRepository.Create(target);
			}
			target.TargetUserId = targetUserId;
			target.UpdatedByUserId = userId;
			target.StatusId = MissionPrimaryTargetUserStatus.Active;
			target.LatestUpdate = DateTime.UtcNow;
			await mRepository.SaveAsync(cancellationToken);
			return new MissionPrimaryTargetUserDto(target);
		}

		public async Task StartMissionAsync(int userId, int missionId, CancellationToken cancellationToken)
		{
			var mission = await mRepository.GetQueryable<Mission>(e => e.Id == missionId).SingleAsync(cancellationToken);
			if (mission.StatusId != MissionStatus.Creating) throw new InvalidOperationException("Mission is not in creating status");
			if (mission.CreatorUserId != userId) throw new InvalidOperationException("Only the creator can start the mission");
			mission.StatusId = MissionStatus.Active;
			mission.Company = new Company()
			{
				Name = mission.Name,
				StatusId = CompanyStatus.Active,
				KindId = CompanyKind.SingleMissionHolder
			};
            await mRepository.SaveAsync(cancellationToken);
		}

		private MissionDto? ToDto(Mission? mission)
		{
			if (mission == null) return null;
			var dto = new MissionDto(mission);
			dto.JoinQrCode = mMediaUrls.ApplyAbsoluteUrl(dto.JoinQrCode);
			return dto;
		}

		private readonly QrCodeBuilder mQrCodeBuilder;
		private readonly IRepository mRepository;
		private readonly IUserService mUsers;
		private readonly ILogger mLogger;
		private readonly MediaUrlResolver mMediaUrls;
		private readonly IMediaInboundProcessor mMediaInboundProcessor;
	}
}
