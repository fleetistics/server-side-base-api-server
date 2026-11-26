using api_server.BusinessLogic.Dto;
using api_server.Controllers.CompanyContext.Dto;
using api_server.Controllers.Fleet.Services;
using api_server.Controllers.MapData.Dto;
using api_server.Controllers.Messages;
using api_server.Controllers.Missions.Dto;
using api_server.Controllers.Companys.Dto;
using api_server.Medias;
using api_server.Missions;
using api_server.Companys;
using api_server.Users;
using db_model.Companies;
using db_model.Gps;
using db_model.Map;
using db_model.Messages;
using db_model.Missions;
using db_model.Company;
using db_model.UserManagement;
using DotLiquid.Tags;
using exs.Commons.Utils;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;
using api_server.Companies;

namespace api_server.CompanyContext
{
	public enum CompanyContextUpdateOutcome
	{
		Updated,
		NotModified,
		CompanyNotFound,
		Forbidden
	}

	public sealed class CompanyContextUpdateResult
    {
		public static readonly CompanyContextUpdateResult CompanyNotFound = new(CompanyContextUpdateOutcome.CompanyNotFound, null);
		public static readonly CompanyContextUpdateResult NotModified = new(CompanyContextUpdateOutcome.NotModified, null);
		public static readonly CompanyContextUpdateResult Forbidden = new(CompanyContextUpdateOutcome.Forbidden, null);

		public static CompanyContextUpdateResult Updated(CompanyContextDelta delta) => new(CompanyContextUpdateOutcome.Updated, delta);

		private CompanyContextUpdateResult(CompanyContextUpdateOutcome outcome, CompanyContextDelta? delta)
		{
			Outcome = outcome;
			Delta = delta;
		}

		public CompanyContextUpdateOutcome Outcome { get; }
		public CompanyContextDelta? Delta { get; }
	}

	public interface ICompanyContextService
    {
		/// <summary>
		/// Full snapshot of everything currently active/visible. <paramref name="currentUserId"/>
		/// must be an active member of the Company (<see cref="CompanyContextUpdateOutcome.Forbidden"/>
		/// otherwise), and is excluded from the returned Members/Users/MobileGpsDevices/
		/// GpsDeviceMapStates/UserAlerts - a caller isn't told about itself.
		/// </summary>
		Task<CompanyContextUpdateResult> GetCompanyContextAsync(int currentUserId, int CompanyId, CancellationToken cancellationToken);

		/// <summary>
		/// Only what changed since <paramref name="latestUpdateDate"/> (unix milliseconds, the
		/// <see cref="CompanyContextDelta.LastUpdate"/> of the previous call), plus
		/// removal markers. Same caller-membership/exclusion rules as <see cref="GetCompanyContextAsync"/>.
		/// </summary>
		Task<CompanyContextUpdateResult> GetCompanyContextDeltaAsync(int currentUserId, int CompanyId, long latestUpdateDate, int[]? notCompanyUserIds, int[]? requestedUserIds, CancellationToken cancellationToken);
	}

	public sealed class CompanyContextService : ICompanyContextService
	{

		public CompanyContextService(IRepository repository, MediaUrlResolver mediaUrls, ILogger<CompanyContextService> logger)
		{
			mRepository = repository;
			mMediaUrls = mediaUrls;
            mLogger = logger;
        }

        // 1. Full snapshot of everything currently active/visible.
        public async Task<CompanyContextUpdateResult> GetCompanyContextAsync(int currentUserId, int CompanyId, CancellationToken cancellationToken)
		{
			var delta = new CompanyContextDelta();

			//check Company is active and get the current server UTC time
            var Company = await mRepository.GetQueryable<Company>(t => t.Id == CompanyId && t.StatusId == CompanyStatus.Active)
						.Select(t => new { Company = t, Now = DateTime.UtcNow })
						.AsNoTracking()
						.FirstOrDefaultAsync(cancellationToken);
			if (Company == null) return CompanyContextUpdateResult.CompanyNotFound;
			delta.CompanyHeader = new CompanyHeaderDto(Company.Company);

			//list of active Company members
            var members = await mRepository.GetQueryable<CompanyMemberUser>(e => e.CompanyId == CompanyId && e.StatusId == CompanyMemberUserStatus.Active)
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
            if (members.FindIndex(x => x.Member.UserId == currentUserId) < 0) return CompanyContextUpdateResult.Forbidden;


            var details = await mRepository.GetQueryable<CompanyDetails>(e => e.CompanyId == CompanyId)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			if (details != null) delta.CompanyDetails = new CompanyDetailsDto(details);

			var media = await mRepository.GetQueryable<CompanyUploadedMedia>(e => e.CompanyId == CompanyId && (e.GroupKey == null || e.GroupKey != -1))
				.Include(e => e.Media)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			if (media.Count > 0) delta.CompanyUploadedMedias = media.Select(toMediaDto).ToList();
			
            //list of users used in messages and activities. Some of them can be not an active member now
			var extraUserIds = new HashSet<int>();

            delta.CompanyPrimaryTargetUser = await mRepository.GetQueryable<CompanyPrimaryTargetUser>(e => e.CompanyId == CompanyId).Select( e=> new CompanyPrimaryTargetUserDto(e)).FirstOrDefaultAsync(cancellationToken);

			if (delta.CompanyPrimaryTargetUser != null)
			{
				if (delta.CompanyPrimaryTargetUser.TargetUserId == null) delta.CompanyPrimaryTargetUser = null;
				else extraUserIds.Add(delta.CompanyPrimaryTargetUser.TargetUserId.Value);
			}

			var messages = await mRepository.GetQueryable<UserMessage>().Include(u => u.UserMessage2Users.Where(u => u.UserId == currentUserId))
                .Where(e => (e.UserId == currentUserId || e.ToUserId == currentUserId || e.ToUserId == null) && (e.CompanyId == CompanyId ))
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

            var CompanyActivities = await mRepository.GetQueryable<CompanyActivity>(e=>e.CompanyId == CompanyId).Include(u => u.CompanyActivity2Users.Where(u => u.UserId == currentUserId)).AsNoTracking().ToListAsync(cancellationToken);
			if (CompanyActivities != null)
			{
                delta.CompanyActivities = new List<CompanyActivityDto>();
                foreach (var activity in CompanyActivities)
                {
                    if (activity.UserId != null && activity.UserId != currentUserId) extraUserIds.Add(activity.UserId.Value);
                    var tmpEntity = new CompanyActivityDto(activity);
                    foreach (var activity2User in activity.CompanyActivity2Users)
                    {
                        if (activity2User.UserId == currentUserId && activity2User.StatusId == UserMessage2UserStatus.Read)
                        {
                            tmpEntity.UserReadStatusId = activity2User.StatusId;
                            break;

                        }
                    }
                    delta.CompanyActivities.Add(tmpEntity);
                }
            }

            delta.Members = new List<CompanyMemberUserDto>();
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

                delta.Members.Add(new CompanyMemberUserDto(row.Member));
				delta.Users.Add(toUserDto(row.User));
			}
			if (delta.Members.Count == 0)
			{
				delta.Members = null;
				delta.UserLocationPrivacies = null;

            }
			else
			{
				delta.MobileGpsDevices = await mRepository.GetQueryable<GpsDevice>(e => e.ProviderId == GpsDeviceProvider.Mobile && e.UserId != null && allMemberIds.Contains(e.UserId.Value) && e.StatusId == GpsDeviceStatus.Active).Select(s => new MobileGpsDeviceDto(s)).ToListAsync(cancellationToken);
                delta.GpsDeviceMapStates = await loadPhoneMapStatesAsync(nonPrivateMemberIds, new List<int>(), DateTime.MinValue, cancellationToken);
				delta.UserAlerts = await mRepository.GetQueryable<UserEmergencyAlert>(e => allMemberIds.Contains(e.UserId) && e.StatusId == UserEmergencyAlertStatus.Active).Select(s => new ActiveUserEmergencyAlertDto(s)).ToListAsync(cancellationToken);

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
            if (delta.UserMessages?.Count == 0) delta.UserMessages = null;
            if (delta.CompanyActivities?.Count == 0) delta.CompanyActivities = null;
            delta.LastUpdate = toCursor(Company.Now);
			return CompanyContextUpdateResult.Updated(delta);
		}

		// 2. Only what changed since `since`, plus removal markers.
		public async Task<CompanyContextUpdateResult> GetCompanyContextDeltaAsync(int currentUserId, int currentCompanyId, CompanyContextRequestData requestData, CancellationToken cancellationToken)
		{
			DateTime since = DateTimeOffset.FromUnixTimeMilliseconds(requestData.LatestUpdateDate).UtcDateTime;
            var delta = new CompanyContextDelta();
            var mission = await mRepository.GetQueryable<Mission>(t => t.Id == currentCompanyId && t.StatusId == CompanyStatus.Active)
				.Select(t => new { Mission = t, Now = DateTime.UtcNow })
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			if (mission == null) return CompanyContextUpdateResult.CompanyNotFound;
            if (mission.Mission.LatestUpdate > since) delta.Mission = new MissionDto(mission.Mission);

            var members = await mRepository.GetQueryable<User2Company>(e => e.CompanyId == currentCompanyId &&
                    (e.StatusId == User2CompanyStatus.Active || e.LatestUpdate > since))
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            if (members.FindIndex(x => x.UserId == currentUserId && x.StatusId == User2CompanyStatus.Active) < 0) return CompanyContextUpdateResult.Forbidden;

			var details = await mRepository.GetQueryable<MissionDetails>(e => e.MissionId == mission.Mission.Id && e.LatestUpdate > since)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			if (details != null) delta.MissionDetails = new MissionDetailsDto(details);

			var media = await mRepository.GetQueryable<MissionUploadedMedia>(e => e.MissionId == mission.Mission.Id && e.LatestUpdate > since)
				.Include(e => e.Media)
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			foreach (var m in media)
			{
				if (m.GroupKey == -1)
					(delta.RemoveMissionMediaIds ??= new()).Add(m.UploadedMediaId);
				else
					(delta.MissionUploadedMedias ??= new()).Add(toMediaDto(m));
			}
            
			var newUserIds = new HashSet<int>(requestData.RequestedUserIds ?? new List<int>());
            var toCheckUpdateUserIds = new HashSet<int>(requestData.ToCheckUpdatesUserIds ?? new List<int>());
            
			var messages = await mRepository.GetQueryable<UserMessage>().Include(u => u.UserMessage2Users.Where(u => u.UserId == currentUserId))
                .Where(e => ((e.UserId == currentUserId || e.ToUserId == currentUserId || e.ToUserId == null) && (e.CompanyId == currentCompanyId)) && e.LatestUpdate > since)
                .AsNoTracking().ToListAsync(cancellationToken);
            if (messages != null)
            {
                delta.UserMessages = new List<UserMessageDto>();
                foreach (var message in messages)
                {
                    if (message.UserId != currentUserId) newUserIds.Add(message.UserId);
                    if (message.ToUserId != null && message.ToUserId != currentUserId) newUserIds.Add(message.ToUserId.Value);
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

            var updatedMissionPrimaryTargetUser = await mRepository.GetQueryable<MissionPrimaryTargetUser>(e => e.MissionId == mission.Mission.Id && e.LatestUpdate > since).Select(e => new MissionPrimaryTargetUserDto(e)).FirstOrDefaultAsync(cancellationToken);
			if (updatedMissionPrimaryTargetUser != null )
			{
				if (updatedMissionPrimaryTargetUser.TargetUserId == null) delta.DoRemoveMissionPrimaryTargetUser = true;
				else {
					delta.MissionPrimaryTargetUser = updatedMissionPrimaryTargetUser;
                    newUserIds.Add(updatedMissionPrimaryTargetUser.TargetUserId.Value);
                }
            }

            newUserIds.ExceptWith(toCheckUpdateUserIds);
            var companyUserIds = new List<int>();

			foreach (var row in members)
			{
				if (row.UserId == currentUserId) continue;
                mLogger.LogInformation($"CompanyMemberUser UserId {row.UserId} StatusId {row.StatusId} LatestUpdate {row.LatestUpdate} since {since} new {(row.LatestUpdate > since)}");
                if (row.LatestUpdate > since)
				{

					if (row.StatusId == User2CompanyStatus.Active)
					{
						(delta.Members ??= new()).Add(new User2CompanyDto(row));

						//in case user is already memmber but updated the roles - to clean all records and load again
                        (delta.RemoveMemberUserIds ??= new()).Add(row.UserId);
						if (!toCheckUpdateUserIds.Contains(row.UserId)) newUserIds.Add(row.UserId);
                        companyUserIds.Add(row.UserId);


                    }
					else
					{
						(delta.RemoveMemberUserIds ??= new()).Add(row.UserId);
                        if (newUserIds.Remove(row.UserId)) toCheckUpdateUserIds.Add(row.UserId);

                    }

				}
				else
				{
					companyUserIds.Add(row.UserId);
                    newUserIds.Remove(row.UserId);
					toCheckUpdateUserIds.Add(row.UserId);
				}
			}
            newUserIds.Remove(currentUserId);
            toCheckUpdateUserIds.Remove(currentUserId);
            if (toCheckUpdateUserIds.Count > 0 || newUserIds.Count > 0)
            {
				mLogger.LogInformation($"load users updateUserIds {string.Join(", ", toCheckUpdateUserIds)}  loadUserIds {string.Join(", ", newUserIds)}");

				var updatedUsers = await mRepository.GetQueryable<User>(e => (toCheckUpdateUserIds.Contains(e.Id) && e.LatestUpdate > since) || newUserIds.Contains(e.Id))
                        .Include(u => u.AvatarImage)
                        .Include(u => u.GovIDImage)
                        .AsNoTracking()
                        .ToListAsync(cancellationToken);
                foreach (var user in updatedUsers) 
                {
                    mLogger.LogInformation($"User UserId {user.Id} StatusId {user.StatusId} LatestUpdate {user.LatestUpdate} since {since} new {(user.LatestUpdate > since)}");
                    if (user.StatusId == UserStatus.Active)
					{
                        (delta.Users ??= new()).Add(toUserDto(user));
                    }
					else
					{
                        if (CompanyUserIds.Contains(user.Id) || loadCompanyUserIds.Contains(user.Id))
						{
                            CompanyUserIds.Remove(user.Id);
                            loadCompanyUserIds.Remove(user.Id);
                            loadMapStateUserIds.Remove(user.Id);
                            loadMemberDetailsUserIds.Add(user.Id);
                            (delta.RemoveMemberUserIds ??= new()).Add(user.Id);
                            if (notCompanyUserIds?.Contains(user.Id) == true || loadNotCompanyUserIds.Contains(user.Id))
                            {
                                (delta.Users ??= new()).Add(toUserDto(user));
                            }
                            else (delta.RemoveUserIds ??= new()).Add(user.Id);

                        }
						else (delta.Users ??= new()).Add(toUserDto(user));

                    }
                }
            }
            var updateMapStateUserIds = new HashSet<int>(CompanyUserIds);

            if (CompanyUserIds.Count > 0 || loadCompanyUserIds.Count > 0)
			{
				delta.UserLocationPrivacies = new List<UserLocationPrivacyDto>();
				delta.RemoveUserLocationPrivacyIds = new List<int>();
				delta.RemoveGpsDeviceMapStateIds = new List<int>();
				
				var privateUsers = await mRepository.GetQueryable<UserLocationPrivacy>(e => CompanyUserIds.Contains(e.UserId) || loadCompanyUserIds.Contains(e.UserId))
					.AsNoTracking()
					.ToListAsync(cancellationToken);
				foreach (var userPrivacy in privateUsers)
				{
					mLogger.LogInformation($"UserLocationPrivacy UserId {userPrivacy.UserId} PrivacyMode {userPrivacy.PrivacyMode} LatestUpdate {userPrivacy.LatestUpdate} since {since} > {(userPrivacy.LatestUpdate > since)}");
					if (userPrivacy.PrivacyMode == 0)
					{
						if (userPrivacy.LatestUpdate > since)
						{
                            loadMapStateUserIds.Add(userPrivacy.UserId);
							delta.RemoveUserLocationPrivacyIds.Add(userPrivacy.UserId);
                            updateMapStateUserIds.Remove(userPrivacy.UserId);
						}
					}
					else {
						if (userPrivacy.LatestUpdate > since)
						{
							delta.UserLocationPrivacies.Add(new UserLocationPrivacyDto(userPrivacy));
							delta.RemoveGpsDeviceMapStateIds.Add(userPrivacy.UserId);

						}
                        loadMapStateUserIds.Remove(userPrivacy.UserId);
                        updateMapStateUserIds.Remove(userPrivacy.UserId);
                    }
				}


            }
			if (updateMapStateUserIds.Count > 0 || loadMapStateUserIds.Count > 0)
			{
                delta.GpsDeviceMapStates = await loadPhoneMapStatesAsync(loadMapStateUserIds, updateMapStateUserIds, since, cancellationToken);
            }
            if (CompanyUserIds.Count > 0 || loadMemberDetailsUserIds.Count > 0)
			{
                var alerts = await mRepository.GetQueryable<UserEmergencyAlert>(e => (CompanyUserIds.Contains(e.UserId) && e.LatestUpdate > since) || loadMemberDetailsUserIds.Contains(e.UserId))
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
                foreach (var alert in alerts)
                {
					if (alert.StatusId == UserEmergencyAlertStatus.Active)
						(delta.UserAlerts ??= new()).Add(new ActiveUserEmergencyAlertDto(alert));
					else
					{
						(delta.RemoveUserAlertIds ??= new()).Add(alert.UserId);
					}
                }

                var devices = await mRepository.GetQueryable<GpsDevice>(e => e.ProviderId == GpsDeviceProvider.Mobile && e.UserId != null && ((CompanyUserIds.Contains(e.UserId.Value) && e.LatestUpdate > since) || loadMemberDetailsUserIds.Contains(e.UserId.Value))).AsNoTracking().ToListAsync(cancellationToken);
                foreach (var device in devices)
                {
                    if (device.StatusId == GpsDeviceStatus.Active)
                        (delta.MobileGpsDevices ??= new()).Add(new MobileGpsDeviceDto(device));
                    else
                    {
                        (delta.RemoveMobileGpsDeviceIds ??= new()).Add(device.Id);
                    }
                }
            }

			if (delta.MobileGpsDevices?.Count == 0) delta.MobileGpsDevices = null;
			if (delta.GpsDeviceMapStates?.Count == 0) delta.GpsDeviceMapStates = null;
			if (delta.UserAlerts?.Count == 0) delta.UserAlerts = null;
			if (delta.UserLocationPrivacies?.Count == 0) delta.UserLocationPrivacies = null;
			if (delta.RemoveUserLocationPrivacyIds?.Count == 0) delta.RemoveUserLocationPrivacyIds = null;
			if (delta.RemoveGpsDeviceMapStateIds?.Count == 0) delta.RemoveGpsDeviceMapStateIds = null;
			if (delta.UserMessages?.Count == 0) delta.UserMessages = null;
			if (delta.CompanyActivities?.Count == 0) delta.CompanyActivities = null;

			if (!hasChanges(delta)) return CompanyContextUpdateResult.NotModified;
            delta.LastUpdate = toCursor(Company.Now);

            return CompanyContextUpdateResult.Updated(delta);
		}
		private UserDto toUserDto(User user)
		{
			var dto = new UserDto(user);
			mMediaUrls.ApplyAbsoluteUrl(dto.AvatarImage);
			mMediaUrls.ApplyAbsoluteUrl(dto.GovIDImage);
			return dto;
		}

		private UploadedMediaDto toMediaDto(MissionUploadedMedia media)
		{
			var dto = new UploadedMediaDto(media.Media!) { GroupKey = media.GroupKey };
			mMediaUrls.ApplyAbsoluteUrl(dto);
			return dto;
		}

		private static bool hasChanges(CompanyContextDelta delta) =>
			delta.CompanyHeader != null ||
			delta.CompanyDetails != null ||
			delta.CompanyUploadedMedias is { Count: > 0 } ||
			delta.RemoveCompanyMediaIds is { Count: > 0 } ||
			delta.Members is { Count: > 0 } ||
			delta.RemoveMemberUserIds is { Count: > 0 } ||
			delta.CompanyPrimaryTargetUser != null ||
			delta.DoRemoveCompanyPrimaryTargetUser == true ||
			delta.Users is { Count: > 0 } ||
			delta.RemoveUserIds is { Count: > 0 } ||
			delta.MobileGpsDevices is { Count: > 0 } ||
			delta.RemoveMobileGpsDeviceIds is { Count: > 0 } ||
			delta.GpsDeviceMapStates is { Count: > 0 } ||
			delta.UserAlerts is { Count: > 0 } ||
			delta.RemoveUserAlertIds is { Count: > 0 } ||
			delta.UserMessages is { Count: > 0 } ||
			delta.RemoveUserMessageIds is { Count: > 0 } ||
			delta.CompanyActivities is { Count: > 0 } ||
			delta.UserLocationPrivacies is { Count: > 0 } ||
			delta.RemoveUserLocationPrivacyIds is { Count: > 0 } ||
			delta.RemoveGpsDeviceMapStateIds is { Count: > 0 };

		// The cursor travels as a plain long of unix MILLISECONDS (not through the whole-second
		// DateTime serializer) and is rounded DOWN, never up. Rounding up would push the cursor past
		// the server's own "now": a row committed after this call's queries, but before the rounded-up
		// instant, has LatestUpdate <= cursor and the next delta (LatestUpdate > since) would skip it
		// forever - e.g. a new member whose map state then arrives with a Member/User record.
		// Rounding down can only make a row stamped within the last millisecond show up once more on
		// the next call, which is harmless: the client applies records as upserts.
		private static long toCursor(DateTime value) =>
			new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

		/// <summary>
		/// Current map states of the users' phones: every state of <paramref name="loadUserIds"/>, plus the states of
		/// <paramref name="updatedUserIds"/> changed after <paramref name="since"/>. A phone is a mobile gps_device whose
		/// UserId says whose it is; its motion activity and battery sit in the state data JSON.
		/// </summary>
		private async Task<List<GpsDeviceMapStateDto>> loadPhoneMapStatesAsync(IEnumerable<int> loadUserIds, IEnumerable<int> updatedUserIds, DateTime since, CancellationToken cancellationToken)
		{
			var load = loadUserIds.ToList();
			var updated = updatedUserIds.ToList();
			var rows = await (
				from s in mRepository.GetQueryable<GpsDeviceMapState>()
				join d in mRepository.GetQueryable<GpsDevice>(x => x.ProviderId == GpsDeviceProvider.Mobile && x.UserId != null) on s.GpsDeviceId equals d.Id
				where load.Contains(d.UserId!.Value) //|| (updated.Contains(d.UserId!.Value) && s.LatestOnMapUpdate > since)
				join sd in mRepository.GetQueryable<GpsDeviceStateData>() on s.GpsDeviceId equals sd.GpsDeviceId into sds
				from sd in sds.DefaultIfEmpty()
				select new { State = s, UserId = d.UserId!.Value, Data = sd != null ? sd.Data : null })
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			return rows.Select(r => new GpsDeviceMapStateDto(r.State, r.UserId, r.Data)).ToList();
		}

		private readonly IRepository mRepository;
		private readonly MediaUrlResolver mMediaUrls;
        private readonly ILogger mLogger;
    }
}
