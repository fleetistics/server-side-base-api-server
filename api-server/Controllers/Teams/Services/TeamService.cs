using api_server.Controllers.Teams.Dto;
using api_server.Service;
using AutoMapper;
using db_model.Team;
using exs.commons.Utils;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Teams.Services
{
	public interface ITeamService
	{
		Task<TeamDto> CreateTeamAsync(int creatorUserId, CreateTeamDto body, CancellationToken cancellationToken);

		/// <summary>Null when no team exists with this id.</summary>
		Task<TeamDto?> GetTeamAsync(int teamId, CancellationToken cancellationToken);

		/// <summary>Applies only the fields present in <paramref name="patch"/>. Null when no team exists with this id.</summary>
		Task<TeamDto?> PatchTeamAsync(int teamId, TeamPatchDto patch, CancellationToken cancellationToken);

		/// <summary>Null when no team exists with this id.</summary>
		Task<TeamDto?> GetTeamDetailsAsync(int teamId, CancellationToken cancellationToken);

		/// <summary>Same field updates as <see cref="PatchTeamAsync"/>, returning the expanded details view. Null when no team exists with this id.</summary>
		Task<TeamDto?> PatchTeamDetailsAsync(int teamId, TeamPatchDto patch, CancellationToken cancellationToken);
	}

	public sealed class TeamService : ITeamService
	{
		public TeamService(QrCodeBuilder qrCodeBuilder, IRepository repository, IMapper mapper, ILogger<TeamService> logger)
		{
			mRepository = repository;
			mQrCodeBuilder = qrCodeBuilder;
			mMapper = mapper;
			mLogger = logger;
		}

		public async Task<TeamDto> CreateTeamAsync(int creatorUserId, CreateTeamDto body, CancellationToken cancellationToken)
		{
			var entity = new Team
			{
				CreatorUserId = creatorUserId,
				CreatedDate = DateTime.UtcNow,
				TeamName = body.TeamName,
				StatusId = TeamStatus.Creating
			};
			mRepository.Create(entity);
			var attempts = 50;
			//test JoinKey duplications
			while (attempts > 0)
			{
				try
				{
					entity.JoinKey = SymbolKeyGenerator.GenerateSymbolKey(6);
					await mRepository.SaveAsync(cancellationToken);
					break;
				}
				catch (Exception ex)
				{
					mLogger.LogError(ex, $"Error on generate joinKey: {entity.JoinKey} attempts {attempts}");
					attempts--;
				}

			}
			entity.JoinQrCode = await mQrCodeBuilder.CreateQrCodeFileAsync(entity.JoinKey, cancellationToken);
			entity.StatusId = TeamStatus.Active;
			// entity.Id is already DB-assigned at this point (from the save above), so
			// team_details' FK is a real value here, not a pending/temporary key.
			//
			// JoinQrCode/StatusId and the team_details row (the only place LatestUpdate,
			// Team's own concurrency stamp, lives) must land together: one SaveChangesAsync
			// call flushes both the modified Team and the newly-added TeamDetails in a single
			// implicit transaction, so a crash here can't leave an Active team with no
			// team_details row, or activate the team without ever writing its QR code.
			var teamDetails = new TeamDetails { TeamId = entity.Id };
			// Convention-based: copies any CreateTeamDto properties an app-specific fork
			// added that share a name/type with a TeamDetails property it also added.
			mMapper.Map(body, teamDetails);
			mRepository.Create(teamDetails);
			await mRepository.SaveAsync(cancellationToken);

			return new TeamDto(entity);
		}

		public async Task<TeamDto?> GetTeamAsync(int teamId, CancellationToken cancellationToken)
		{
			var entity = await mRepository.GetQueryable<Team>(e => e.Id == teamId)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			return entity == null ? null : new TeamDto(entity);
		}

		public async Task<TeamDto?> PatchTeamAsync(int teamId, TeamPatchDto patch, CancellationToken cancellationToken)
		{
			var entity = await mRepository.GetQueryable<Team>(e => e.Id == teamId)
				.FirstOrDefaultAsync(cancellationToken);
			if (entity == null) return null;

			if (patch.TeamName.IsSet) entity.TeamName = patch.TeamName.Value!;
			if (patch.StatusId.IsSet) entity.StatusId = patch.StatusId.Value;
			if (patch.ClosedDate.IsSet) entity.ClosedDate = patch.ClosedDate.Value;
			if (patch.JoinKey.IsSet) entity.JoinKey = patch.JoinKey.Value;

			await mRepository.SaveAsync(cancellationToken);
			return new TeamDto(entity);
		}

		public async Task<TeamDto?> GetTeamDetailsAsync(int teamId, CancellationToken cancellationToken)
		{
			var team = await mRepository.GetQueryable<Team>(e => e.Id == teamId)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			if (team == null) return null;

			var details = await mRepository.GetQueryable<TeamDetails>(e => e.TeamId == teamId)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);

			var dto = new TeamDto(team);
			if (details != null) dto.AssignDetails(details);
			return dto;
		}

		public async Task<TeamDto?> PatchTeamDetailsAsync(int teamId, TeamPatchDto patch, CancellationToken cancellationToken)
		{
			var team = await PatchTeamAsync(teamId, patch, cancellationToken);
			if (team == null) return null;

			return await GetTeamDetailsAsync(teamId, cancellationToken);
		}

		// TODO: stub relocated here (unchanged) from a syntax error introduced by an
		// edit outside this session - it was declared directly in the namespace body,
		// between the interface and this class, which isn't valid C#. Body still empty;
		// nothing about its logic has been filled in.
		protected Task joinNewTeamSwitchFromPreviousTeam(int userId, Team newTeam, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		private readonly QrCodeBuilder mQrCodeBuilder;
		private readonly IRepository mRepository;
		private readonly IMapper mMapper;
		private readonly ILogger mLogger;
	}
}
