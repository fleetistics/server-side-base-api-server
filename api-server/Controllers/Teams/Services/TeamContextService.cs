using api_server.BusinessLogic.Dto;
using api_server.Controllers.MapData.Dto;
using api_server.Controllers.Media;
using api_server.Controllers.Media.Dto;
using api_server.Controllers.Messages;
using api_server.Controllers.Teams.Dto;
using api_server.Controllers.Users.Dto;
using db_model.Gps;
using db_model.Map;
using db_model.Messages;
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
		Task<TeamContextUpdateResult> GetTeamContextDeltaAsync(int currentUserId, int teamId, long latestUpdateDate, int[]? notTeamUserIds, int[]? requestedUserIds, CancellationToken cancellationToken);
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

			//check team is active and get the current server UTC time
            var team = await mRepository.GetQueryable<Team>(t => t.Id == teamId && t.StatusId == TeamStatus.Active)
						.Select(t => new { Team = t, Now = DateTime.UtcNow })
						.AsNoTracking()
						.FirstOrDefaultAsync(cancellationToken);
			if (team == null) return TeamContextUpdateResult.TeamNotFound;
			delta.TeamHeader = new TeamHeaderDto(team.Team);

			//list of active team members
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

            //check the current user is an active member
            if (members.FindIndex(x => x.Member.UserId == currentUserId) < 0) return TeamContextUpdateResult.Forbidden;


            var details = await mRepository.GetQueryable<TeamDetails>(e => e.TeamId == teamId)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			if (details != null) delta.TeamDetails = new TeamDetailsDto(details);

			var media = await mRepository.GetQueryable<TeamUploadedMedia>(e => e.TeamId == teamId && (e.GroupKey == null || e.GroupKey != -1))
				.Include(e => e.Media)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			if (media.Count > 0) delta.TeamUploadedMedias = media.Select(toMediaDto).ToList();
			
            //list of users used in messages and activities. Some of them can be not an active member now
			var extraUserIds = new HashSet<int>();

            delta.TeamPrimaryTargetUser = await mRepository.GetQueryable<TeamPrimaryTargetUser>(e => e.TeamId == teamId).Select( e=> new TeamPrimaryTargetUserDto(e)).FirstOrDefaultAsync(cancellationToken);

			if (delta.TeamPrimaryTargetUser != null)
			{
				if (delta.TeamPrimaryTargetUser.TargetUserId == null) delta.TeamPrimaryTargetUser = null;
				else extraUserIds.Add(delta.TeamPrimaryTargetUser.TargetUserId.Value);
			}

			var messages = await mRepository.GetQueryable<UserMessage>().Include(u => u.UserMessage2Users.Where(u => u.UserId == currentUserId))
                .Where(e => (e.UserId == currentUserId || e.ToUserId == currentUserId || e.ToUserId == null) && (e.TeamId == teamId || e.TeamId == null))
				.AsNoTracking().ToListAsync(cancellationToken);

			if(messages != null)
			{
				delta.UserMessages = new List<UserMessageDto>();
				foreach( var message in messages)
				{
					if (message.UserId != currentUserId) extraUserIds.Add(message.UserId);
                    if (message.ToUserId != null && message.ToUserId != currentUserId) extraUserIds.Add(message.ToUserId.Value);
                    var tmpMessage = new UserMessageDto(message);
					foreach(var message2User in message.UserMessage2Users)
					{
						if (message2User.UserId == currentUserId && message2User.StatusId == UserMessage2UserStatus.Read)
						{
							tmpMessage.UserReadStatusId = message2User.StatusId;
							break;

                        }
					}
					delta.UserMessages.Add(tmpMessage);
                }
            }

            var teamActivities = await mRepository.GetQueryable<TeamActivity>(e=>e.TeamId == teamId).Include(u => u.TeamActivity2Users.Where(u => u.UserId == currentUserId)).AsNoTracking().ToListAsync(cancellationToken);
			if (teamActivities != null)
			{
                delta.TeamActivities = new List<TeamActivityDto>();
                foreach (var activity in teamActivities)
                {
                    if (activity.UserId != null && activity.UserId != currentUserId) extraUserIds.Add(activity.UserId.Value);
                    var tmpEntity = new TeamActivityDto(activity);
                    foreach (var activity2User in activity.TeamActivity2Users)
                    {
                        if (activity2User.UserId == currentUserId && activity2User.StatusId == UserMessage2UserStatus.Read)
                        {
                            tmpEntity.UserReadStatusId = activity2User.StatusId;
                            break;

                        }
                    }
                    delta.TeamActivities.Add(tmpEntity);
                }
            }

            delta.Members = new List<TeamMemberUserDto>();
			delta.Users = new List<UserDto>();
            delta.UserLocationPrivacies = new List<UserLocationPrivacyDto>();
            var allMemberIds = new HashSet<int>();
            var nonPrivateMemberIds = new HashSet<int>();
            foreach (var row in members)
			{
				if (row.Member.UserId == currentUserId ) continue;
                allMemberIds.Add(row.Member.UserId);

                if (row.User.LocationPrivacy?.PrivacyMode > 0) delta.UserLocationPrivacies.Add( new UserLocationPrivacyDto(row.User.LocationPrivacy));
                else nonPrivateMemberIds.Add(row.User.Id);
				extraUserIds.Remove(row.User.Id);

                delta.Members.Add(new TeamMemberUserDto(row.Member));
				delta.Users.Add(toUserDto(row.User));
			}
			if (delta.Members.Count == 0)
			{
				delta.Members = null;
				delta.UserLocationPrivacies = null;

            }
			else
			{
				delta.MobileGpsDevices = await loadAllDevicesAsync(allMemberIds, cancellationToken);
				delta.GpsDeviceMapStates = await loadMapStatesAsync(nonPrivateMemberIds, since: null, cancellationToken);
				delta.UserAlerts = await loadActiveAlertsAsync(allMemberIds, since: null, cancellationToken);

				// loadDevicesAsync/etc. always return a list, even when empty - null it back out
				// so an empty collection is omitted from the wire, same as every sibling field here.
				if (delta.MobileGpsDevices.Count == 0) delta.MobileGpsDevices = null;
				if (delta.GpsDeviceMapStates.Count == 0) delta.GpsDeviceMapStates = null;
				if (delta.UserAlerts.Count == 0) delta.UserAlerts = null;
                if (delta.UserLocationPrivacies.Count == 0) delta.UserLocationPrivacies = null;
            }
			if (extraUserIds.Count > 0)
			{
				if (extraUserIds.Count > 0)
				{
					var users = await mRepository.GetQueryable<User>(u => extraUserIds.Contains(u.Id)).Include(u => u.AvatarImage).Include(u => u.GovIDImage).AsNoTracking().ToListAsync(cancellationToken);
					foreach (var tmpUser in users)
					{
						delta.Users.Add(toUserDto(tmpUser));
					}
				}
			}
            if (delta.Users.Count == 0)
            {
                delta.Users = null;
            }
            delta.LastUpdate = roundUpToSecond(team.Now);
			return TeamContextUpdateResult.Updated(delta);
		}

		// 2. Only what changed since `since`, plus removal markers.
		public async Task<TeamContextUpdateResult> GetTeamContextDeltaAsync(int currentUserId, int teamId, long latestUpdateDate, int[]? notTeamUserIds, int[]? requestedUserIds, CancellationToken cancellationToken)
		{
			DateTime since = DateTimeHelper.UnixTimeToUtcDateTime(latestUpdateDate);
            var delta = new TeamContextDelta();
            var team = await mRepository.GetQueryable<Team>(t => t.Id == teamId && t.StatusId == TeamStatus.Active)
				.Select(t => new { Team = t, Now = DateTime.UtcNow })
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			if (team == null) return TeamContextUpdateResult.TeamNotFound;

            var members = await mRepository.GetQueryable<TeamMemberUser>(e => e.TeamId == teamId &&
                    (e.StatusId == TeamMemberUserStatus.Active || e.LatestUpdate > since))
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            if (members.FindIndex(x => x.UserId == currentUserId && x.StatusId == TeamMemberUserStatus.Active) < 0) return TeamContextUpdateResult.Forbidden;

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

            var extraUserIds = new HashSet<int>(requestedUserIds ?? Array.Empty<int>());
            var messages = await mRepository.GetQueryable<UserMessage>().Include(u => u.UserMessage2Users.Where(u => u.UserId == currentUserId))
                .Where(e => ((e.UserId == currentUserId || e.ToUserId == currentUserId || e.ToUserId == null) && (e.TeamId == teamId || e.TeamId == null)) && e.LatestUpdate > since)
                .AsNoTracking().ToListAsync(cancellationToken);
            if (messages != null)
            {
                delta.UserMessages = new List<UserMessageDto>();
                foreach (var message in messages)
                {
                    if (message.UserId != currentUserId) extraUserIds.Add(message.UserId);
                    if (message.ToUserId != null && message.ToUserId != currentUserId) extraUserIds.Add(message.ToUserId.Value);
                    var tmpMessage = new UserMessageDto(message);
                    foreach (var message2User in message.UserMessage2Users)
                    {
                        if (message2User.UserId == currentUserId && message2User.StatusId == UserMessage2UserStatus.Read)
                        {
                            tmpMessage.UserReadStatusId = message2User.StatusId;
                            break;

                        }
                    }
                    delta.UserMessages.Add(tmpMessage);
                }
            }
            var teamActivities = await mRepository.GetQueryable<TeamActivity>(e => e.TeamId == teamId && e.Date > since).Include(u => u.TeamActivity2Users.Where(u => u.UserId == currentUserId)).AsNoTracking().ToListAsync(cancellationToken);
            if (teamActivities != null)
            {
                delta.TeamActivities = new List<TeamActivityDto>();
                foreach (var activity in teamActivities)
                {
                    if (activity.UserId != null && activity.UserId != currentUserId) extraUserIds.Add(activity.UserId.Value);
                    var tmpEntity = new TeamActivityDto(activity);
                    foreach (var activity2User in activity.TeamActivity2Users)
                    {
                        if (activity2User.UserId == currentUserId && activity2User.StatusId == UserMessage2UserStatus.Read)
                        {
                            tmpEntity.UserReadStatusId = activity2User.StatusId;
                            break;

                        }
                    }
                    delta.TeamActivities.Add(tmpEntity);
                }
            }

            var updatedTeamPrimaryTargetUser = await mRepository.GetQueryable<TeamPrimaryTargetUser>(e => e.TeamId == teamId && e.LatestUpdate > since).Select(e => new TeamPrimaryTargetUserDto(e)).FirstOrDefaultAsync(cancellationToken);
			if (updatedTeamPrimaryTargetUser != null )
			{
				if (updatedTeamPrimaryTargetUser.TargetUserId == null) delta.DoRemoveTeamPrimaryTargetUser = true;
				else {
					delta.TeamPrimaryTargetUser = updatedTeamPrimaryTargetUser;
                    extraUserIds.Add(updatedTeamPrimaryTargetUser.TargetUserId.Value);
                }
            }

            var teamUserIds = new List<int>();
            HashSet<int> nonPrivateMemberIds = null;
            var fullReloadUserIds = new List<int>();
            //var tmpUsers = new List<User>();
            var userIdToRemoveCandidate = new HashSet<int>();
            foreach (var row in members)
			{
				if (row.UserId == currentUserId) continue;
                if (row.LatestUpdate > since)
				{
					if (row.StatusId == TeamMemberUserStatus.Active)
					{
						(delta.Members ??= new()).Add(new TeamMemberUserDto(row));
						fullReloadUserIds.Add(row.UserId);
                        extraUserIds.Remove(row.UserId);
                    }
					else
					{
						(delta.RemoveMemberUserIds ??= new()).Add(row.UserId);
                        userIdToRemoveCandidate.Add(row.UserId);

                    }

				}
				else teamUserIds.Add(row.UserId);
            }
            var fullReloadMapStateIds = new HashSet<int>(teamUserIds);
            if (teamUserIds.Count > 0)
			{
				delta.UserLocationPrivacies = new List<UserLocationPrivacyDto>();
				delta.RemoveUserLocationPrivacyIds = new List<int>();
				delta.RemoveGpsDeviceMapStateIds = new List<int>();
				nonPrivateMemberIds = new HashSet<int>(teamUserIds);
				
				var privateUsers = await mRepository.GetQueryable<UserLocationPrivacy>(e => teamUserIds.Contains(e.UserId))
					.AsNoTracking()
					.ToListAsync(cancellationToken);
				foreach (var userPrivacy in privateUsers)
				{
					if (userPrivacy.PrivacyMode == 0)
					{
						if (userPrivacy.LatestUpdate > since)
						{
							fullReloadMapStateIds.Add(userPrivacy.UserId);
							delta.RemoveUserLocationPrivacyIds.Add(userPrivacy.UserId);
							nonPrivateMemberIds.Remove(userPrivacy.UserId);
						}
					}
					else {
						if (userPrivacy.LatestUpdate > since)
						{
							delta.UserLocationPrivacies.Add(new UserLocationPrivacyDto(userPrivacy));
							delta.RemoveGpsDeviceMapStateIds.Add(userPrivacy.UserId);

						}
						nonPrivateMemberIds.Remove(userPrivacy.UserId);
						//(delta.RemoveMemberUserIds ??= new()).Add(userPrivacy.UserId);
						//userIdToRemoveCandidate.Add(userPrivacy.UserId);
					}
				}

			}
			if (teamUserIds.Count > 0 || notTeamUserIds?.Length > 0)
			{
				var tmpUserIds = new HashSet<int>(teamUserIds);
				if (notTeamUserIds?.Length > 0) tmpUserIds.UnionWith(notTeamUserIds);
            var updatedUsers = await mRepository.GetQueryable<User>(e => tmpUserIds.Contains(e.Id) && e.LatestUpdate > since)
					.Include(u => u.AvatarImage)
					.Include(u => u.GovIDImage)
					.AsNoTracking()
					.ToListAsync(cancellationToken);
				foreach (var user in updatedUsers)
				{
					if (user.StatusId != UserStatus.Active)
					{
						if (teamUserIds.Contains(user.Id))
						{
							teamUserIds.Remove(user.Id);
							nonPrivateMemberIds?.Remove(user.Id);
							(delta.RemoveMemberUserIds ??= new()).Add(user.Id);
							if (extraUserIds.Contains(user.Id))
							{
								(delta.Users ??= new()).Add(toUserDto(user));
								extraUserIds.Remove(user.Id);
							}
							else userIdToRemoveCandidate.Add(user.Id);
						}
						else
						{
							(delta.Users ??= new()).Add(toUserDto(user));
							extraUserIds.Remove(user.Id);
						}
					}
					else
					{
						(delta.Users ??= new()).Add(toUserDto(user));
                        extraUserIds.Remove(user.Id);
                    }
				}
			}
            if (teamUserIds.Count > 0)
            {

                var (updatesDevices, removeDeviceIds) = await loadDeltaDevicesAsync(teamUserIds, since, cancellationToken);
                if (updatesDevices != null) (delta.MobileGpsDevices ??= new()).AddRange(updatesDevices);
                delta.RemoveMobileGpsDeviceIds = removeDeviceIds;
				if (nonPrivateMemberIds?.Count > 0) (delta.GpsDeviceMapStates ??= new()).AddRange(await loadMapStatesAsync(nonPrivateMemberIds, since, cancellationToken));

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
            if (fullReloadMapStateIds.Count > 0) (delta.GpsDeviceMapStates ??= new()).AddRange(await loadMapStatesAsync(fullReloadMapStateIds, since: null, cancellationToken));

			if (notTeamUserIds?.Length > 0) extraUserIds.UnionWith(notTeamUserIds);
			if (requestedUserIds?.Length > 0) extraUserIds.UnionWith(requestedUserIds);
            if (fullReloadUserIds.Count > 0)
			{
                (delta.RemoveUserIds ??= new()).AddRange(fullReloadUserIds);
                var fullReloadUsers = await mRepository.GetQueryable<User>(e => fullReloadUserIds.Contains(e.Id))
					.Include(u => u.AvatarImage)
					.Include(u => u.GovIDImage)
					.Include(u => u.LocationPrivacy)
					.AsNoTracking()
					.ToListAsync(cancellationToken);
				foreach (var user in fullReloadUsers)
				{
					if (user.StatusId != UserStatus.Active )
					{
                        fullReloadUserIds.Remove(user.Id);
                        if (extraUserIds.Contains(user.Id) )
                        {
                            (delta.Users ??= new()).Add(toUserDto(user));
                            extraUserIds.Remove(user.Id);
                        }
					}
					else
					{
                        (delta.Users ??= new()).Add(toUserDto(user));
                        extraUserIds.Remove(user.Id);

                        if (user.LocationPrivacy?.PrivacyMode > 0)
						{
							delta.UserLocationPrivacies.Add(new UserLocationPrivacyDto(user.LocationPrivacy));
						}
					}



                    if (user.StatusId != UserStatus.Active || user.LocationPrivacy?.PrivacyMode > 0)
					{
						fullReloadUserIds.Remove(user.Id);
						(delta.RemoveMemberUserIds ??= new()).Add(user.Id);
						if (extraUserIds.Contains(user.Id) )
						{
							(delta.Users ??= new()).Add(toUserDto(user));
							extraUserIds.Remove(user.Id);
                        }

					}
					else
					{
						(delta.Users ??= new()).Add(toUserDto(user));
						extraUserIds.Remove(user.Id);
					}
                }
				var (updatesDevices, removeDeviceIds) = await loadDeltaDevicesAsync(fullReloadUserIds, since, cancellationToken);
                if (updatesDevices != null) (delta.MobileGpsDevices ??= new()).AddRange(updatesDevices);
				delta.RemoveMobileGpsDeviceIds = removeDeviceIds;
                (delta.GpsDeviceMapStates ??= new()).AddRange(await loadMapStatesAsync(fullReloadUserIds, since: null, cancellationToken) );
				(delta.UserAlerts ??= new()).AddRange(await loadActiveAlertsAsync(fullReloadUserIds, since: null, cancellationToken) );
			}

			if (extraUserIds.Count > 0)
			{

                    (delta.Users ??= new()).AddRange( await mRepository.GetQueryable<User>(e => extraUserIds.Contains(e.Id))
                    .Include(u => u.AvatarImage)
                    .Include(u => u.GovIDImage)
					.Select( e=> toUserDto(e))
                    .ToListAsync(cancellationToken));
                userIdToRemoveCandidate.ExceptWith(extraUserIds);
            }

            if (userIdToRemoveCandidate.Count > 0)
            {
                (delta.RemoveUserIds ??= new()).AddRange(userIdToRemoveCandidate);
            }

			if (delta.MobileGpsDevices?.Count == 0) delta.MobileGpsDevices = null;
			if (delta.GpsDeviceMapStates?.Count == 0) delta.GpsDeviceMapStates = null;
			if (delta.UserAlerts?.Count == 0) delta.UserAlerts = null;

			

			if (!hasChanges(delta)) return TeamContextUpdateResult.NotModified;
            delta.LastUpdate = roundUpToSecond(team.Now);

            return TeamContextUpdateResult.Updated(delta);
		}

		private async Task<List<MobileGpsDeviceDto>> loadAllDevicesAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken)
		{
			var devices = await mRepository.GetQueryable<MobileGpsDevice>(e => e.UserId != null && userIds.Contains(e.UserId!.Value) && e.StatusId == GpsDeviceStatus.Active)
				.Select(s => new MobileGpsDeviceDto(s)).ToListAsync(cancellationToken);
            return devices.Count > 0 ? devices : new();
		}
        private async Task<(List<MobileGpsDeviceDto>? updatesDevices, List<int>? removeDeviceIds)> loadDeltaDevicesAsync(IReadOnlyCollection<int> userIds, DateTime since, CancellationToken cancellationToken)
        {
			var query = await mRepository.GetQueryable<MobileGpsDevice>(e => e.UserId != null && userIds.Contains(e.UserId!.Value) && e.LatestUpdate > since)
				.AsNoTracking().ToListAsync(cancellationToken);
			if (query != null)
			{
				var updatesDevices = new List<MobileGpsDeviceDto>();
				var removeDeviceIds = new List<int>();
				foreach (var device in query)
				{
					if (device.StatusId == GpsDeviceStatus.Active) updatesDevices.Add(new MobileGpsDeviceDto(device));
					else removeDeviceIds.Add(device.Id);
				}
				return (updatesDevices.Count > 0 ? updatesDevices : null, removeDeviceIds:(removeDeviceIds.Count > 0 ? removeDeviceIds : null));
			}
			else return (null, null);
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
			delta.RemoveTeamMediaIds is { Count: > 0 } ||
			delta.Members is { Count: > 0 } ||
			delta.RemoveMemberUserIds is { Count: > 0 } ||
			delta.TeamPrimaryTargetUser != null ||
			delta.DoRemoveTeamPrimaryTargetUser == true ||
			delta.Users is { Count: > 0 } ||
			delta.RemoveUserIds is { Count: > 0 } ||
			delta.MobileGpsDevices is { Count: > 0 } ||
			delta.RemoveMobileGpsDeviceIds is { Count: > 0 } ||
			delta.GpsDeviceMapStates is { Count: > 0 } ||
			delta.UserAlerts is { Count: > 0 } ||
			delta.RemoveUserAlertIds is { Count: > 0 } ||
			delta.UserMessages is { Count: > 0 } ||
			delta.RemoveUserMessageIds is { Count: > 0 } ||
			delta.TeamActivities is { Count: > 0 };

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
