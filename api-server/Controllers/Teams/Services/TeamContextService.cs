using api_server.BusinessLogic.Dto;
using api_server.Controllers.MapData.Dto;
using api_server.Controllers.Media;
using api_server.Controllers.Media.Dto;
using api_server.Controllers.Teams.Dto;
using api_server.Controllers.Users.Dto;
using db_model.Gps;
using db_model.Map;
using db_model.Team;
using db_model.UserManagement;
using exs.Commons.Utils;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Teams.Services
{
	public enum TeamContextUpdateOutcome
	{
		Updated,
		NotModified,
		TeamNotFound,
		Forbidden
	}

	public sealed class TeamContextUpdateResult
	{
		public static readonly TeamContextUpdateResult TeamNotFound = new(TeamContextUpdateOutcome.TeamNotFound, null);
		public static readonly TeamContextUpdateResult NotModified = new(TeamContextUpdateOutcome.NotModified, null);
		public static readonly TeamContextUpdateResult Forbidden = new(TeamContextUpdateOutcome.Forbidden, null);

		public static TeamContextUpdateResult Updated(TeamContextDelta delta) => new(TeamContextUpdateOutcome.Updated, delta);

		private TeamContextUpdateResult(TeamContextUpdateOutcome outcome, TeamContextDelta? delta)
		{
			Outcome = outcome;
			Delta = delta;
		}

		public TeamContextUpdateOutcome Outcome { get; }
		public TeamContextDelta? Delta { get; }
	}

	public interface ITeamContextService
	{
		/// <summary>
		/// Full snapshot of everything currently active/visible. <paramref name="currentUserId"/>
		/// must be an active member of the team (<see cref="TeamContextUpdateOutcome.Forbidden"/>
		/// otherwise), and is excluded from the returned Members/Users/MobileGpsDevices/
		/// GpsDeviceMapStates/UserAlerts - a caller isn't told about itself.
		/// </summary>
		Task<TeamContextUpdateResult> GetTeamContextAsync(int currentUserId, int teamId, CancellationToken cancellationToken);

		/// <summary>
		/// Only what changed since <paramref name="latestUpdateDate"/> (unix seconds), plus
		/// removal markers. Same caller-membership/exclusion rules as <see cref="GetTeamContextAsync"/>.
		/// </summary>
		Task<TeamContextUpdateResult> GetTeamContextDeltaAsync(int currentUserId, int teamId, long latestUpdateDate, CancellationToken cancellationToken);
	}

	public sealed class TeamContextService : ITeamContextService
	{

		public TeamContextService(IRepository repository, MediaUrlResolver mediaUrls)
		{
			mRepository = repository;
			mMediaUrls = mediaUrls;
		}

        // 1. Full snapshot of everything currently active/visible.
        public async Task<TeamContextUpdateResult> GetTeamContextAsync(int currentUserId, int teamId, CancellationToken cancellationToken)
		{
			var delta = new TeamContextDelta();
            // Capturing DateTime.UtcNow here - inside a query the Npgsql provider translates -
            // pushes down to the database's own now(), not the app server's clock, so LastUpdate
            // never depends on app-server/DB clock skew across however many app instances are
            // running behind the load balancer.
            var team = await mRepository.GetQueryable<Team>(t => t.Id == teamId && t.StatusId == TeamStatus.Active)
				.Select(t => new { Team = t, Now = DateTime.UtcNow })
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			if (team == null) return TeamContextUpdateResult.TeamNotFound;

			delta.TeamHeader = new TeamHeaderDto(team.Team);

			var details = await mRepository.GetQueryable<TeamDetails>(e => e.TeamId == teamId)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			if (details != null) delta.TeamDetails = new TeamDetailsDto(details);

			var media = await mRepository.GetQueryable<TeamUploadedMedia>(e => e.TeamId == teamId && (e.GroupKey == null || e.GroupKey != -1))
				.Include(e => e.Media)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			if (media.Count > 0) delta.TeamUploadedMedias = media.Select(toMediaDto).ToList();

			// Single query: TeamMemberUser inner-joined to User (Active only), which carries its
			// AvatarImage/GovIDImage/LocationPrivacy along as ordinary Include outer joins.
			var members = await mRepository.GetQueryable<TeamMemberUser>(e => e.TeamId == teamId && e.StatusId == TeamMemberUserStatus.Active)
				.Join(
					mRepository.GetQueryable<User>(u => u.StatusId == UserStatus.Active)
						.Include(u => u.AvatarImage)
						.Include(u => u.GovIDImage)
						.Include(u => u.LocationPrivacy),
					m => m.UserId,
					u => u.Id,
					(m, u) => new { Member = m, User = u })
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			// The caller must be an active member of the team, and is excluded from everything
			// derived from this list below - it isn't told about itself (TeamContextDelta.txt:9-11,27-29).
			if (members.FindIndex(x => x.Member.UserId == currentUserId) < 0) return TeamContextUpdateResult.Forbidden;

			delta.Members = new List<TeamMemberDto>();
			delta.Users = new List<UserDto>();
			foreach (var row in members)
			{
				// Users who've opted into location privacy are left out of the initial snapshot
				// entirely (see TeamContextDelta.txt:24) - the User.StatusId==Active requirement
				// from the same line is already enforced by the join above.
				if (row.Member.UserId == currentUserId || row.User.LocationPrivacy?.PrivacyMode > 0) continue;

				delta.Members.Add(new TeamMemberDto(row.Member));
				delta.Users.Add(toUserDto(row.User));
			}
			if (delta.Members.Count == 0)
			{
				delta.Members = null;
				delta.Users = null;
			}
			else
			{
				var visibleUserIds = delta.Users.Select(u => u.Id).ToList();
				delta.MobileGpsDevices = await loadDevicesAsync(visibleUserIds, since: null, cancellationToken);
				delta.GpsDeviceMapStates = await loadMapStatesAsync(visibleUserIds, since: null, cancellationToken);
				delta.UserAlerts = await loadActiveAlertsAsync(visibleUserIds, since: null, cancellationToken);

				// loadDevicesAsync/etc. always return a list, even when empty - null it back out
				// so an empty collection is omitted from the wire, same as every sibling field here.
				if (delta.MobileGpsDevices.Count == 0) delta.MobileGpsDevices = null;
				if (delta.GpsDeviceMapStates.Count == 0) delta.GpsDeviceMapStates = null;
				if (delta.UserAlerts.Count == 0) delta.UserAlerts = null;
			}
			delta.LastUpdate = roundUpToSecond(team.Now);
			return TeamContextUpdateResult.Updated(delta);
		}

		// 2. Only what changed since `since`, plus removal markers.
		public async Task<TeamContextUpdateResult> GetTeamContextDeltaAsync(int currentUserId, int teamId, long latestUpdateDate, CancellationToken cancellationToken)
		{
			DateTime since = DateTimeHelper.UnixTimeToUtcDateTime(latestUpdateDate);
            var delta = new TeamContextDelta();
            var team = await mRepository.GetQueryable<Team>(t => t.Id == teamId && t.StatusId == TeamStatus.Active)
				.Select(t => new { Team = t, Now = DateTime.UtcNow })
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			if (team == null) return TeamContextUpdateResult.TeamNotFound;

			if (team.Team.LatestUpdate > since) delta.TeamHeader = new TeamHeaderDto(team.Team);

			var details = await mRepository.GetQueryable<TeamDetails>(e => e.TeamId == teamId && e.LatestUpdate > since)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			if (details != null) delta.TeamDetails = new TeamDetailsDto(details);

			var media = await mRepository.GetQueryable<TeamUploadedMedia>(e => e.TeamId == teamId && e.LatestUpdate > since)
				.Include(e => e.Media)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			foreach (var m in media)
			{
				if (m.GroupKey == -1)
					(delta.RemoveTeamMediaIds ??= new()).Add(m.UploadedMediaId);
				else
					(delta.TeamUploadedMedias ??= new()).Add(toMediaDto(m));
			}

			var members = await mRepository.GetQueryable<TeamMemberUser>(e => e.TeamId == teamId &&
					(e.StatusId == TeamMemberUserStatus.Active || e.LatestUpdate > since))
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			if (members.FindIndex(x => x.UserId == currentUserId && x.StatusId == TeamMemberUserStatus.Active) < 0) return TeamContextUpdateResult.Forbidden;

			// teamUserIds: members whose membership/privacy/user status is still steady as of
			// `since` - they only need the batch alert/map-state/device refresh further below.
			// fullReloadUserIds: members that need a fresh, unconditional reload of everything.
			var teamUserIds = new List<int>();
			var fullReloadUserIds = new List<int>();

			foreach (var row in members)
			{
				if (row.UserId == currentUserId) continue;

				if (row.LatestUpdate > since)
				{
					if (row.StatusId == TeamMemberUserStatus.Active)
					{
						(delta.Members ??= new()).Add(new TeamMemberDto(row));
						fullReloadUserIds.Add(row.UserId);
					}
					else
					{
						(delta.RemoveUserIds ??= new()).Add(row.UserId);
					}
					// Either way this member's already been placed in the correct bucket above -
					// it must not also fall through into teamUserIds below, or it (and any of its
					// devices/map-states/alerts) would be processed and reported twice.
					continue;
				}
				teamUserIds.Add(row.UserId);
			}

			if (teamUserIds.Count > 0)
			{
				var privateUsers = await mRepository.GetQueryable<UserLocationPrivacy>(e => teamUserIds.Contains(e.UserId) && e.LatestUpdate > since)
					.AsNoTracking()
					.ToListAsync(cancellationToken);
				foreach (var userPrivacy in privateUsers)
				{
					// Whichever way this resolves, the member leaves the steady-state batch below -
					// either it's getting a full reload instead, or it's being removed outright and
					// must not have its (now supposed to be hidden) devices/alerts/map-state sent
					// alongside that removal.
					teamUserIds.Remove(userPrivacy.UserId);
					if (userPrivacy.PrivacyMode == 0)
						fullReloadUserIds.Add(userPrivacy.UserId);
					else
						(delta.RemoveUserIds ??= new()).Add(userPrivacy.UserId);
				}

				if (teamUserIds.Count > 0)
				{
					var updatedUsers = await mRepository.GetQueryable<User>(e => teamUserIds.Contains(e.Id) && e.LatestUpdate > since)
						.Include(u => u.AvatarImage)
						.Include(u => u.GovIDImage)
						.AsNoTracking()
						.ToListAsync(cancellationToken);
					foreach (var user in updatedUsers)
					{
						if (user.StatusId != UserStatus.Active)
						{
							// No longer an active user - drop out of the steady-state batch too,
							// or their stale devices/alerts would still be sent alongside the removal.
							teamUserIds.Remove(user.Id);
							(delta.RemoveUserIds ??= new()).Add(user.Id);
						}
						else
						{
							(delta.Users ??= new()).Add(toUserDto(user));
						}
					}
				}
			}

			if (fullReloadUserIds.Count > 0)
			{
				var fullReloadUsers = await mRepository.GetQueryable<User>(e => fullReloadUserIds.Contains(e.Id))
					.Include(u => u.AvatarImage)
					.Include(u => u.GovIDImage)
					.Include(u => u.LocationPrivacy)
					.AsNoTracking()
					.ToListAsync(cancellationToken);
				foreach (var user in fullReloadUsers)
				{
					// Either condition means the client must be told to remove this member, not
					// just silently receive nothing about them - they were already visible from a
					// previous sync (that's why they ended up in a full reload in the first place).
					if (user.StatusId != UserStatus.Active || user.LocationPrivacy?.PrivacyMode > 0)
					{
						fullReloadUserIds.Remove(user.Id);
						(delta.RemoveUserIds ??= new()).Add(user.Id);
						continue;
					}
					(delta.Users ??= new()).Add(toUserDto(user));
				}

				(delta.MobileGpsDevices ??= new()).AddRange(await loadDevicesAsync(fullReloadUserIds, since: null, cancellationToken) );
				(delta.GpsDeviceMapStates ??= new()).AddRange(await loadMapStatesAsync(fullReloadUserIds, since: null, cancellationToken) );
				(delta.UserAlerts ??= new()).AddRange(await loadActiveAlertsAsync(fullReloadUserIds, since: null, cancellationToken) );
			}

			if (teamUserIds.Count > 0)
			{

                (delta.MobileGpsDevices ??= new()).AddRange(await loadDevicesAsync(teamUserIds, since, cancellationToken) );
				(delta.GpsDeviceMapStates ??= new()).AddRange(await loadMapStatesAsync(teamUserIds, since, cancellationToken) );

				var alerts = await mRepository.GetQueryable<UserEmergencyAlert>(e => teamUserIds.Contains(e.UserId) && e.LatestUpdate > since)
					.AsNoTracking()
					.ToListAsync(cancellationToken);
				foreach (var alert in alerts)
				{
					if (alert.StatusId == UserEmergencyAlertStatus.Active)
						(delta.UserAlerts ??= new()).Add(new ActiveUserEmergencyAlertDto(alert));
					else
						(delta.RemoveUserAlertIds ??= new()).Add(alert.Id);
				}
			}

			if (delta.MobileGpsDevices?.Count == 0) delta.MobileGpsDevices = null;
			if (delta.GpsDeviceMapStates?.Count == 0) delta.GpsDeviceMapStates = null;
			if (delta.UserAlerts?.Count == 0) delta.UserAlerts = null;

			delta.LastUpdate = roundUpToSecond(team.Now);

			if (!hasChanges(delta)) return TeamContextUpdateResult.NotModified;

			return TeamContextUpdateResult.Updated(delta);
		}

		private async Task<List<MobileGpsDeviceDto>> loadDevicesAsync(IReadOnlyCollection<int> userIds, DateTime? since, CancellationToken cancellationToken)
		{
			var query = mRepository.GetQueryable<MobileGpsDevice>(e => e.UserId != null && userIds.Contains(e.UserId!.Value));
			if (since.HasValue)
			{
				var sinceValue = since.Value;
				query = query.Where(e => e.LatestUpdate > sinceValue);
			}
			var devices = await query.AsNoTracking().ToListAsync(cancellationToken);
			return devices.Count > 0 ? devices.Select(s => new MobileGpsDeviceDto(s)).ToList() : new();
		}

		private async Task<List<MobileGpsDeviceMapStateDto>> loadMapStatesAsync(IReadOnlyCollection<int> userIds, DateTime? since, CancellationToken cancellationToken)
		{
			var query = mRepository.GetQueryable<MobileGpsDeviceMapState>(e => userIds.Contains(e.UserId));
			if (since.HasValue)
			{
				var sinceValue = since.Value;
				query = query.Where(e => e.LatestOnMapUpdate > sinceValue);
			}
			var states = await query.AsNoTracking().ToListAsync(cancellationToken);
			return states.Count > 0 ? states.Select(s => new MobileGpsDeviceMapStateDto(s)).ToList() : new();
		}

		private async Task<List<ActiveUserEmergencyAlertDto>> loadActiveAlertsAsync(IReadOnlyCollection<int> userIds, DateTime? since, CancellationToken cancellationToken)
		{
			var query = mRepository.GetQueryable<UserEmergencyAlert>(e => userIds.Contains(e.UserId) && e.StatusId == UserEmergencyAlertStatus.Active);
			if (since.HasValue)
			{
				var sinceValue = since.Value;
				query = query.Where(e => e.LatestUpdate > sinceValue);
			}
			var alerts = await query.AsNoTracking().ToListAsync(cancellationToken);
			return alerts.Count > 0 ? alerts.Select(s => new ActiveUserEmergencyAlertDto(s)).ToList() : new();
		}

		private UserDto toUserDto(User user)
		{
			var dto = new UserDto(user);
			mMediaUrls.ApplyAbsoluteUrl(dto.AvatarImage);
			mMediaUrls.ApplyAbsoluteUrl(dto.GovIDImage);
			return dto;
		}

		private UploadedMediaDto toMediaDto(TeamUploadedMedia media)
		{
			var dto = new UploadedMediaDto(media.Media!) { GroupKey = media.GroupKey };
			mMediaUrls.ApplyAbsoluteUrl(dto);
			return dto;
		}

		private static bool hasChanges(TeamContextDelta delta) =>
			delta.TeamHeader != null ||
			delta.TeamDetails != null ||
			delta.TeamUploadedMedias is { Count: > 0 } ||
			delta.Members is { Count: > 0 } ||
			delta.Users is { Count: > 0 } ||
			delta.MobileGpsDevices is { Count: > 0 } ||
			delta.GpsDeviceMapStates is { Count: > 0 } ||
			delta.UserAlerts is { Count: > 0 } ||
			delta.RemoveUserIds is { Count: > 0 } ||
			delta.RemoveUserAlertIds is { Count: > 0 } ||
			delta.RemoveTeamMediaIds is { Count: > 0 };

		// TeamContextDelta.LastUpdate round-trips through the API as a whole-second unix
		// timestamp (DateTimeNullable2UnixSerializer truncates). Rounding up here - rather than
		// leaving team.Now's full precision, which would floor on the wire - guarantees every
		// record this call could have reported has LatestUpdate <= this value, so a client that
		// echoes it back as its next latestUpdateDate will never see an already-reported record
		// (including this call's own baseline rows) look "changed" again. The cost is symmetrical,
		// not one-sided: a genuinely new change landing in the same second this rounds up into is
		// simply picked up on the client's next poll instead of this one - normal, bounded (< 1s)
		// polling latency, not a missed update.
		private static DateTime roundUpToSecond(DateTime value)
		{
			var truncated = value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
			return truncated == value ? truncated : truncated.AddSeconds(1);
		}

		private readonly IRepository mRepository;
		private readonly MediaUrlResolver mMediaUrls;
	}
}
