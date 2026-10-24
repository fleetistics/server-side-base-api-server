using api_server.Controllers.Teams.Dto;
using api_server.Users;
using api_server.core;
using api_server.Medias;
using api_server.Service;
using api_server.Teams;
using AutoMapper;
using db_model.Media;
using db_model.Team;
using db_model.UserManagement;
using exs.commons.Utils;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace api_server.Controllers.Teams.Services
{
	public interface ITeamService
	{
		/// <summary>Id of the user's Active team (as an Active member), or null when they have none.</summary>
		Task<int?> GetUserActiveTeamIdAsync(int userId, CancellationToken cancellationToken);

		/// <summary>The team the user is still setting up (Creating status), or null when there is none.</summary>
		Task<TeamCreateDto?> GetCreatingTeamByUserAsync(int userId, CancellationToken cancellationToken);

		/// <summary>
		/// Creates a team in Creating status with its join key and QR code, applies the patch parts
		/// (details, creator user, creator member) and registers the creator as its first member.
		/// Returns the new team id.
		/// </summary>
		Task<int> CreateTeamAsync(int userId, TeamCreatePatchDto createTeam, CancellationToken cancellationToken);

		/// <summary>Applies only the fields present in <paramref name="createTeam"/> to a team still in Creating status.</summary>
		Task PatchCreateTeamAsync(int userId, int teamId, TeamCreatePatchDto createTeam, CancellationToken cancellationToken);

		/// <summary>Activates a Creating team; only its creator can start it.</summary>
		Task StartTeamAsync(int userId, int teamId, CancellationToken cancellationToken);
	}

	public sealed class TeamService : ITeamService
	{
        public async Task<int?> GetUserActiveTeamIdAsync(int userId, CancellationToken cancellationToken)
        {
            var teamId = await mRepository.GetQueryable<TeamMemberUser>(e => e.UserId == userId && e.StatusId == TeamMemberUserStatus.Active && e.Team.StatusId == TeamStatus.Active).Select(e => (int?)e.TeamId).FirstOrDefaultAsync(cancellationToken);
            return teamId;
        }

        public async Task<TeamCreateDto?> GetCreatingTeamByUserAsync(int userId, CancellationToken cancellationToken)
		{
            var team = await mRepository.GetQueryable<Team>(e => e.CreatorUserId == userId && (e.StatusId == TeamStatus.Creating || e.StatusId == TeamStatus.GenerateKey)).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (team == null) return null;
			if (team.StatusId == TeamStatus.GenerateKey)
			{
				mRepository.Delete(team);
                await mRepository.SaveAsync(cancellationToken);
				return null;

            }
            var details = await mRepository.GetQueryable<TeamDetails>(e => e.TeamId == team.Id).Include(e=>e.Medias).ThenInclude(e=> e.Media).Select(d => new TeamDetailsDto(d)).FirstOrDefaultAsync(cancellationToken);
			foreach(var teamMedia in details?.Medias ?? Enumerable.Empty<TeamMediaDto>()) mMediaUrls.ApplyAbsoluteUrl(teamMedia.Media);
            var creatorTeamMemberUser = await mRepository.GetQueryable<TeamMemberUser>(e => e.TeamId == team.Id && e.UserId == userId).Select(t => new TeamMemberUserDto(t)).FirstOrDefaultAsync(cancellationToken);
            var teamDto = new TeamDto(team);
            teamDto.JoinQrCode = mMediaUrls.ApplyAbsoluteUrl(teamDto.JoinQrCode);
            return new TeamCreateDto()
			{
				Team = teamDto,
                TeamDetails = details,
                CreatorTeamMemberUser = creatorTeamMemberUser
            };
        }
        public async Task<int> CreateTeamAsync(int userId, TeamCreatePatchDto createTeam, CancellationToken cancellationToken)
		{
            var entity = new Team
            {
                CreatorUserId = userId,
                CreatedDate = DateTime.UtcNow,
                StatusId = TeamStatus.GenerateKey
            };
            // TeamName is NOT NULL, so it has to be on the entity before the first insert below.
            entity.ApplyPatch(createTeam.Team!);
            mRepository.Create(entity);
            var attempts = 50;
            //test JoinKey duplications
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
            entity.StatusId = TeamStatus.Creating;
            entity.JoinQrCode = await mQrCodeBuilder.CreateQrCodeFileAsync(entity.JoinKey, cancellationToken);
            

			var teamDetails = new TeamDetails { TeamId = entity.Id };
			if (createTeam.TeamDetails != null)
			{
				teamDetails.ApplyPatch(createTeam.TeamDetails);
				
                var insertedMedias = await mMediaInboundProcessor.ProcessAsync(createTeam.TeamDetails, [], cancellationToken);
                if (insertedMedias.Any())
                {
                    foreach (var newMedia in insertedMedias)
                    {
                        var teamMedia = new TeamUploadedMedia
                        {
                            TeamId = entity.Id,
                            UploadedMediaId = newMedia.Id,
                            GroupKey = createTeam.TeamDetails.InsertMedias?.Find(e => e.Guid == newMedia.Guid)?.GroupKey
                        };
                        mRepository.Create(teamMedia);
                    }
                }

            }
            mRepository.Create(teamDetails);
            if (createTeam.CreatorUser != null)
			{
                await mUsers.PatchUserAsync(userId, createTeam.CreatorUser, cancellationToken);
            }
            var teamMemberUser = new TeamMemberUser
            {
                TeamId = entity.Id,
                UserId = userId,
                StatusId = TeamMemberUserStatus.Creating,
                RoleId = TeamUserRole.Creator
            };
            mRepository.Create(teamMemberUser);

            if (createTeam.CreatorTeamMemberUser != null)
			{
                teamMemberUser.ApplyPatch(createTeam.CreatorTeamMemberUser!);
            }
            await mRepository.SaveAsync(cancellationToken);
            return entity.Id;
        }
        public async Task PatchCreateTeamAsync(int userId, int teamId, TeamCreatePatchDto createTeam, CancellationToken cancellationToken)
		{
            var team = await mRepository.GetQueryable<Team>(e => e.Id == teamId).SingleAsync(cancellationToken);
			if (team.StatusId != TeamStatus.Creating) throw new InvalidOperationException("Team is not in creating status");
            if (createTeam.Team != null) team.ApplyPatch(createTeam.Team);
            if (createTeam.TeamDetails != null)
            {
                var teamDetails = await mRepository.GetQueryable<TeamDetails>(e => e.TeamId == teamId).Include(e => e.Medias).SingleAsync(cancellationToken);
                teamDetails.ApplyPatch(createTeam.TeamDetails);
                var insertedMedias = await mMediaInboundProcessor.ProcessAsync(createTeam.TeamDetails, teamDetails.Medias?.Select(m => m.UploadedMediaId).ToList() ?? [], cancellationToken);
				if (createTeam.TeamDetails.RemoveMediaIds != null && teamDetails.Medias?.Count > 0)
				{
					foreach(var teamMedia in teamDetails.Medias)
					{
						if (createTeam.TeamDetails.RemoveMediaIds.Contains(teamMedia.UploadedMediaId)) mRepository.Delete(teamMedia);
                    }
                }
                if (insertedMedias.Any())
                {
					foreach(var newMedia in insertedMedias)
					{
						var teamMedia = new TeamUploadedMedia
                        {
                            TeamId = teamId,
                            UploadedMediaId = newMedia.Id,
                            GroupKey = createTeam.TeamDetails.InsertMedias?.Find(e => e.Guid == newMedia.Guid)?.GroupKey
                        };
                        mRepository.Create(teamMedia);
                    }
                }
            }
            if (createTeam.CreatorUser != null)
            {
                await mUsers.PatchUserAsync(userId, createTeam.CreatorUser, cancellationToken);
            }
            if (createTeam.CreatorTeamMemberUser != null)
            {
                var teamMemberUser = await mRepository.GetQueryable<TeamMemberUser>(e => e.TeamId == teamId && e.UserId == userId).SingleOrDefaultAsync(cancellationToken);
                if (teamMemberUser == null)
                {
                    teamMemberUser = new TeamMemberUser
                    {
                        TeamId = teamId,
                        UserId = userId,
                        StatusId = TeamMemberUserStatus.Creating,
                        RoleId = TeamUserRole.Creator
                    };
                    mRepository.Create(teamMemberUser);
                }
                teamMemberUser.ApplyPatch(createTeam.CreatorTeamMemberUser!);
            }
            await mRepository.SaveAsync(cancellationToken);
        }

        public async Task StartTeamAsync(int userId, int teamId, CancellationToken cancellationToken)
		{
            var team = await mRepository.GetQueryable<Team>(e => e.Id == teamId).SingleAsync(cancellationToken);
            if (team.StatusId != TeamStatus.Creating) throw new InvalidOperationException("Team is not in creating status");
			if ( team.CreatorUserId != userId) throw new InvalidOperationException("Only the creator can start the team");
            var teamMemberUser = await mRepository.GetQueryable<TeamMemberUser>(e => e.TeamId == teamId && e.UserId == userId).SingleOrDefaultAsync(cancellationToken);
            if (teamMemberUser == null ) throw new InvalidOperationException("User is not a member of the team");
            var otherTeamMemberUsers = await mRepository.GetQueryable<TeamMemberUser>(e => e.TeamId != teamId && e.UserId == userId && e.StatusId == TeamMemberUserStatus.Active).ToListAsync(cancellationToken);
			foreach(var otherTeamMemberUser in otherTeamMemberUsers)
            {
                otherTeamMemberUser.StatusId = TeamMemberUserStatus.WithOtherTeam;
            }
            teamMemberUser.StatusId = TeamMemberUserStatus.Active;
			team.StatusId = TeamStatus.Active;
            await mRepository.SaveAsync(cancellationToken);

            var notes = await mRepository.GetQueryable<TeamDetails>(e => e.TeamId == teamId).Select(e => e.Notes).FirstOrDefaultAsync(cancellationToken);
            if (mMockTeamMemberMove.IsMockTeamNotes(notes)) mMockTeamMemberMove.Start(teamId, userId);
        }

        public TeamService(QrCodeBuilder qrCodeBuilder, MediaUrlResolver mediaUrls, IMediaInboundProcessor mediaInboundProcessor, IRepository repository, IMapper mapper, IUserService users, IMockTeamMemberMove mockTeamMemberMove, ILogger<TeamService> logger)
		{
			mMockTeamMemberMove = mockTeamMemberMove;
			mUsers = users;
			mRepository = repository;
			mQrCodeBuilder = qrCodeBuilder;
			mMapper = mapper;
			mLogger = logger;
            mMediaUrls = mediaUrls;
            mMediaInboundProcessor = mediaInboundProcessor;	
        }



		private readonly QrCodeBuilder mQrCodeBuilder;
		private readonly IRepository mRepository;
		private readonly IMapper mMapper;
		private readonly IUserService mUsers;
		private readonly IMockTeamMemberMove mMockTeamMemberMove;
		private readonly ILogger mLogger;

        private readonly MediaUrlResolver mMediaUrls;
        private readonly IMediaInboundProcessor mMediaInboundProcessor;
    }
}
