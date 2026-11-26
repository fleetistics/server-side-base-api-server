using api_server.Controllers.Teams.Dto;
using api_server.Users;
using api_server.core;
using api_server.Medias;
using api_server.Service;
using api_server.Teams;
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
        public TeamService(MediaUrlResolver mediaUrls, IRepository repository)
		{
			mRepository = repository;
            mMediaUrls = mediaUrls;
        }

		private readonly IRepository mRepository;
        private readonly MediaUrlResolver mMediaUrls;
    }
}
