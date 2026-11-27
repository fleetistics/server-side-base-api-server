using api_server.Services;
using api_server.Controllers.Contacts.Dto;
using db_model.Companies;
using db_model.Contacts;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Contacts.Services
{
	/// <summary>Which occurrences an edit or a cancel reaches.</summary>
	public enum ActivityScope
	{
		/// <summary>Only the activity itself.</summary>
		This = 1,
		/// <summary>The activity and the later ones of its series that are still Scheduled.</summary>
		ThisAndFollowing = 2,
	}

	/// <summary>The filters of the activity list. From and To are Unix times in seconds (From included, To excluded).</summary>
	public class ActivityListQuery
	{
		public int? CompanyId { get; set; }
		public int? ContactUserId { get; set; }
		/// <summary>A colleague's activity history: the activities about them, assigned to them or created by them.</summary>
		public int? PersonUserId { get; set; }
		public long? From { get; set; }
		public long? To { get; set; }
		public short? TypeId { get; set; }
		public short? PriorityId { get; set; }
		public short? StatusId { get; set; }
		public int? CreatorUserId { get; set; }
		/// <summary>The newest activities first, at most this many (default 500, at most 1000).</summary>
		public int? Limit { get; set; }
	}

	/// <summary>
	/// Calls, meetings, emails and visits of the caller's company with its companies and people (Contacts ▸ Activities).
	/// What the caller sees: the activities of their company that are not private to another user. An activity is private to its creator
	/// when the creator asked for that or when its company or contact is private.
	/// A recurring activity is made as one row per occurrence (up to 100), all pointing to one series row.
	/// </summary>
	public interface IActivitiesService
	{
		Task<ActivityLookupsDto> GetLookupsAsync(int ownerCompanyId, CancellationToken cancellationToken);
		Task<IReadOnlyList<ActivityRowDto>> ListAsync(int ownerCompanyId, int userId, ActivityListQuery query, CancellationToken cancellationToken);
		Task<ContactResult<ActivityDto>> GetAsync(int ownerCompanyId, int userId, int activityId, CancellationToken cancellationToken);
		Task<ContactResult<ActivityDto>> CreateAsync(int ownerCompanyId, int userId, SaveActivityDto request, CancellationToken cancellationToken);
		Task<ContactResult<ActivityDto>> UpdateAsync(int ownerCompanyId, int userId, int activityId, SaveActivityDto request, ActivityScope scope, CancellationToken cancellationToken);
		Task<ContactResult<ActivityDto>> CompleteAsync(int ownerCompanyId, int userId, int activityId, CancellationToken cancellationToken);
		Task<ContactResult<ActivityDto>> CancelAsync(int ownerCompanyId, int userId, int activityId, ActivityScope scope, CancellationToken cancellationToken);
	}

	public sealed class ActivitiesService : IActivitiesService
	{
		private const int MaxOccurrences = 100;
		private const int DefaultLimit = 500;
		private const int MaxLimit = 1000;

		public ActivitiesService(IRepository repository, TimeProvider time)
		{
			mRepository = repository;
			mTime = time;
		}

		// ---- lookups ----

		public async Task<ActivityLookupsDto> GetLookupsAsync(int ownerCompanyId, CancellationToken cancellationToken)
		{
			var result = new ActivityLookupsDto
			{
				// Every list is sorted by name.
				Types = SortedByName((await mRepository.GetQueryable<ActivityType>().AsNoTracking().ToListAsync(cancellationToken)).Select(t => new LookupItemDto { Id = t.Id, Name = t.Name })),
				Priorities = SortedByName((await mRepository.GetQueryable<ActivityPriority>().AsNoTracking().ToListAsync(cancellationToken)).Select(t => new LookupItemDto { Id = t.Id, Name = t.Name })),
				Statuses = SortedByName((await mRepository.GetQueryable<ActivityStatus>().AsNoTracking().ToListAsync(cancellationToken)).Select(t => new LookupItemDto { Id = t.Id, Name = t.Name })),
				Frequencies = SortedByName((await mRepository.GetQueryable<RecurrenceFrequency>().AsNoTracking().ToListAsync(cancellationToken)).Select(t => new LookupItemDto { Id = t.Id, Name = t.Name })),
			};
			var memberIds = await MemberIdsAsync(ownerCompanyId, cancellationToken);
			var names = await UserNamesAsync(memberIds, cancellationToken);
			result.Users = names.Select(n => new UserLookupDto { Id = n.Key, Name = n.Value }).OrderBy(u => u.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
			return result;
		}

		// ---- read ----

		public async Task<IReadOnlyList<ActivityRowDto>> ListAsync(int ownerCompanyId, int userId, ActivityListQuery query, CancellationToken cancellationToken)
		{
			var activities = VisibleActivities(ownerCompanyId, userId).AsNoTracking();
			if (query.CompanyId is int companyId) activities = activities.Where(a => a.CompanyId == companyId);
			if (query.ContactUserId is int contactId) activities = activities.Where(a => a.ContactUserId == contactId);
			if (query.PersonUserId is int personId) activities = activities.Where(a => a.ContactUserId == personId || a.AssignedToUserId == personId || a.CreatedByUserId == personId);
			if (query.From is long from) { var f = FromUnix(from); activities = activities.Where(a => a.StartAt >= f); }
			if (query.To is long to) { var t = FromUnix(to); activities = activities.Where(a => a.StartAt < t); }
			if (query.TypeId is short typeId) activities = activities.Where(a => a.TypeId == typeId);
			if (query.PriorityId is short priorityId) activities = activities.Where(a => a.PriorityId == priorityId);
			if (query.StatusId is short statusId) activities = activities.Where(a => a.StatusId == statusId);
			if (query.CreatorUserId is int creatorId) activities = activities.Where(a => a.CreatedByUserId == creatorId);

			var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);
			var list = await activities.OrderByDescending(a => a.StartAt).ThenByDescending(a => a.Id).Take(limit).ToListAsync(cancellationToken);
			return await ToDtosAsync<ActivityRowDto>(list, cancellationToken);
		}

		public async Task<ContactResult<ActivityDto>> GetAsync(int ownerCompanyId, int userId, int activityId, CancellationToken cancellationToken)
		{
			var activity = await VisibleActivities(ownerCompanyId, userId).AsNoTracking().SingleOrDefaultAsync(a => a.Id == activityId, cancellationToken);
			if (activity == null) return ContactResult<ActivityDto>.NotFound("There is no such activity.");
			return ContactResult<ActivityDto>.Ok(await ToDtoAsync(activity, cancellationToken));
		}

		// ---- write ----

		public async Task<ContactResult<ActivityDto>> CreateAsync(int ownerCompanyId, int userId, SaveActivityDto request, CancellationToken cancellationToken)
		{
			var now = Now();
			var start = AsUtc(request.StartAt);
			var error = await ValidateAsync(ownerCompanyId, request, cancellationToken);
			if (error != null) return ContactResult<ActivityDto>.Invalid(error);
			if (request.Log && request.Recurrence != null) return ContactResult<ActivityDto>.Invalid("A logged activity cannot repeat.");
			if (request.Log && start > now.AddMinutes(5)) return ContactResult<ActivityDto>.Invalid("A logged activity has already happened: pick a time that is not in the future.");

			var target = await ResolveTargetAsync(ownerCompanyId, userId, request, cancellationToken);
			if (target.Error != null) return ContactResult<ActivityDto>.Invalid(target.Error);

			var starts = new List<DateTime> { start };
			ActivitySeries? series = null;
			if (request.Recurrence != null)
			{
				var recurrenceError = ValidateRecurrence(request.Recurrence, start);
				if (recurrenceError != null) return ContactResult<ActivityDto>.Invalid(recurrenceError);
				starts = Occurrences(request.Recurrence, start);
				if (starts.Count < 2) return ContactResult<ActivityDto>.Invalid("The repeat ends before the second occurrence.");
				series = new ActivitySeries
				{
					OwnerCompanyId = ownerCompanyId,
					FrequencyId = request.Recurrence.FrequencyId,
					Interval = request.Recurrence.Interval,
					EndAfterCount = request.Recurrence.EndAfterCount,
					EndOnDate = request.Recurrence.EndOnDate,
					CreationDate = now,
					LatestUpdate = now,
				};
				mRepository.Create(series);
				await mRepository.SaveAsync(cancellationToken);
			}

			var ownerUserId = request.IsPrivate || target.IsPrivate ? userId : (int?)null;
			var created = new List<Activity>();
			try
			{
				foreach (var at in starts)
				{
					var activity = new Activity
					{
						OwnerCompanyId = ownerCompanyId,
						OwnerUserId = ownerUserId,
						TypeId = request.TypeId,
						Subject = request.Subject.Trim(),
						Notes = Blank(request.Notes),
						PriorityId = request.PriorityId ?? ActivityPriority.Normal,
						StatusId = request.Log ? ActivityStatus.Completed : ActivityStatus.Scheduled,
						CompanyId = target.CompanyId,
						ContactUserId = request.ContactUserId,
						AssignedToUserId = request.AssignedToUserId ?? userId,
						CreatedByUserId = userId,
						StartAt = at,
						DurationMinutes = request.DurationMinutes,
						CompletedAt = request.Log ? at : null,
						SeriesId = series?.Id,
						CreationDate = now,
						LatestUpdate = now,
					};
					mRepository.Create(activity);
					created.Add(activity);
				}
				await mRepository.SaveAsync(cancellationToken);
			}
			catch
			{
				if (series != null) await TryRemoveSeriesAsync(series.Id);
				throw;
			}
			return ContactResult<ActivityDto>.Ok(await ToDtoAsync(created[0], cancellationToken));
		}

		public async Task<ContactResult<ActivityDto>> UpdateAsync(int ownerCompanyId, int userId, int activityId, SaveActivityDto request, ActivityScope scope, CancellationToken cancellationToken)
		{
			var activity = await VisibleActivities(ownerCompanyId, userId).SingleOrDefaultAsync(a => a.Id == activityId, cancellationToken);
			if (activity == null) return ContactResult<ActivityDto>.NotFound("There is no such activity.");
			if (activity.StatusId == ActivityStatus.Cancelled) return ContactResult<ActivityDto>.Invalid("A cancelled activity cannot be edited.");

			var error = await ValidateAsync(ownerCompanyId, request, cancellationToken);
			if (error != null) return ContactResult<ActivityDto>.Invalid(error);
			var target = await ResolveTargetAsync(ownerCompanyId, userId, request, cancellationToken);
			if (target.Error != null) return ContactResult<ActivityDto>.Invalid(target.Error);

			var now = Now();
			var newStart = AsUtc(request.StartAt);
			var delta = newStart - activity.StartAt;
			var oldStart = activity.StartAt;

			var affected = new List<Activity> { activity };
			if (scope == ActivityScope.ThisAndFollowing && activity.SeriesId is int seriesId)
				affected.AddRange(await mRepository.GetQueryable<Activity>(a => a.SeriesId == seriesId && a.Id != activity.Id && a.StartAt > oldStart && a.StatusId == ActivityStatus.Scheduled && a.OwnerCompanyId == ownerCompanyId).ToListAsync(cancellationToken));

			foreach (var a in affected)
			{
				a.TypeId = request.TypeId;
				a.Subject = request.Subject.Trim();
				a.Notes = Blank(request.Notes);
				a.PriorityId = request.PriorityId ?? ActivityPriority.Normal;
				a.CompanyId = target.CompanyId;
				a.ContactUserId = request.ContactUserId;
				a.AssignedToUserId = request.AssignedToUserId ?? a.AssignedToUserId;
				a.DurationMinutes = request.DurationMinutes;
				// The later occurrences move by the same amount, so the series keeps its rhythm.
				a.StartAt = a == activity ? newStart : a.StartAt + delta;
				if (a.StatusId == ActivityStatus.Completed) a.CompletedAt = a.CompletedAt ?? a.StartAt;
				a.LatestUpdate = now;
			}
			await mRepository.SaveAsync(cancellationToken);
			return ContactResult<ActivityDto>.Ok(await ToDtoAsync(activity, cancellationToken));
		}

		public async Task<ContactResult<ActivityDto>> CompleteAsync(int ownerCompanyId, int userId, int activityId, CancellationToken cancellationToken)
		{
			var activity = await VisibleActivities(ownerCompanyId, userId).SingleOrDefaultAsync(a => a.Id == activityId, cancellationToken);
			if (activity == null) return ContactResult<ActivityDto>.NotFound("There is no such activity.");
			if (activity.StatusId == ActivityStatus.Cancelled) return ContactResult<ActivityDto>.Invalid("A cancelled activity cannot be completed.");
			if (activity.StatusId != ActivityStatus.Completed)
			{
				var now = Now();
				activity.StatusId = ActivityStatus.Completed;
				activity.CompletedAt = now;
				activity.LatestUpdate = now;
				await mRepository.SaveAsync(cancellationToken);
			}
			return ContactResult<ActivityDto>.Ok(await ToDtoAsync(activity, cancellationToken));
		}

		public async Task<ContactResult<ActivityDto>> CancelAsync(int ownerCompanyId, int userId, int activityId, ActivityScope scope, CancellationToken cancellationToken)
		{
			var activity = await VisibleActivities(ownerCompanyId, userId).SingleOrDefaultAsync(a => a.Id == activityId, cancellationToken);
			if (activity == null) return ContactResult<ActivityDto>.NotFound("There is no such activity.");
			if (activity.StatusId == ActivityStatus.Completed) return ContactResult<ActivityDto>.Invalid("A completed activity cannot be cancelled.");

			var now = Now();
			var affected = new List<Activity> { activity };
			if (scope == ActivityScope.ThisAndFollowing && activity.SeriesId is int seriesId)
			{
				var from = activity.StartAt;
				affected.AddRange(await mRepository.GetQueryable<Activity>(a => a.SeriesId == seriesId && a.Id != activity.Id && a.StartAt > from && a.StatusId == ActivityStatus.Scheduled && a.OwnerCompanyId == ownerCompanyId).ToListAsync(cancellationToken));
			}
			foreach (var a in affected)
			{
				a.StatusId = ActivityStatus.Cancelled;
				a.LatestUpdate = now;
			}
			await mRepository.SaveAsync(cancellationToken);
			return ContactResult<ActivityDto>.Ok(await ToDtoAsync(activity, cancellationToken));
		}

		// ---- validation ----

		private async Task<string?> ValidateAsync(int ownerCompanyId, SaveActivityDto request, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(request.Subject)) return "Enter a subject.";
			if (request.StartAt == default || request.StartAt.Year < 2000 || request.StartAt.Year > 2100) return "Enter the date and time.";
			if (request.DurationMinutes < 0 || request.DurationMinutes > 1440) return "The duration is 0 to 1440 minutes.";
			if (!await mRepository.GetQueryable<ActivityType>(t => t.Id == request.TypeId).AnyAsync(cancellationToken)) return "Unknown activity type.";
			if (request.PriorityId is short pid && !await mRepository.GetQueryable<ActivityPriority>(t => t.Id == pid).AnyAsync(cancellationToken)) return "Unknown priority.";
			if (request.AssignedToUserId is int uid && !(await MemberIdsAsync(ownerCompanyId, cancellationToken)).Contains(uid)) return "The activity can only be assigned to a member of your company.";
			return null;
		}

		private static string? ValidateRecurrence(ActivityRecurrenceDto r, DateTime start)
		{
			if (r.FrequencyId < RecurrenceFrequency.Daily || r.FrequencyId > RecurrenceFrequency.Yearly) return "Unknown repeat unit.";
			if (r.Interval < 1 || r.Interval > 99) return "Repeat every 1 to 99.";
			if ((r.EndAfterCount == null) == (r.EndOnDate == null)) return "Choose when the repeat ends: after a number of times or on a date.";
			if (r.EndAfterCount is int n && (n < 2 || n > MaxOccurrences)) return $"The repeat ends after 2 to {MaxOccurrences} times.";
			if (r.EndOnDate is DateOnly end && end < DateOnly.FromDateTime(start)) return "The repeat cannot end before it starts.";
			return null;
		}

		/// <summary>The start times of every occurrence (the first is <paramref name="start"/>), at most 100.</summary>
		private static List<DateTime> Occurrences(ActivityRecurrenceDto r, DateTime start)
		{
			var result = new List<DateTime>();
			for (var i = 0; i < MaxOccurrences; i++)
			{
				if (r.EndAfterCount is int count && i >= count) break;
				// Counted from the start every time, so a month-end day does not drift (31 Jan, 28 Feb, 31 Mar).
				var at = r.FrequencyId switch
				{
					RecurrenceFrequency.Daily => start.AddDays(i * r.Interval),
					RecurrenceFrequency.Weekly => start.AddDays(7 * i * r.Interval),
					RecurrenceFrequency.Monthly => start.AddMonths(i * r.Interval),
					_ => start.AddYears(i * r.Interval),
				};
				if (r.EndOnDate is DateOnly end && DateOnly.FromDateTime(at) > end) break;
				result.Add(at);
			}
			return result;
		}

		/// <summary>
		/// The company the activity is about. A contact brings their company; a colleague belongs to the caller's company.
		/// Whether it is private: its company or contact is.
		/// </summary>
		private async Task<(int CompanyId, bool IsPrivate, string? Error)> ResolveTargetAsync(int ownerCompanyId, int userId, SaveActivityDto request, CancellationToken cancellationToken)
		{
			var isPrivate = false;
			var companyId = request.CompanyId;

			if (request.ContactUserId is int personId)
			{
				var isColleague = (await MemberIdsAsync(ownerCompanyId, cancellationToken)).Contains(personId);
				int personCompany;
				if (isColleague) personCompany = ownerCompanyId;
				else
				{
					var link = await mRepository.GetQueryable<CompanyUserContact>(c => c.OwnerCompanyId == ownerCompanyId && c.ContactUserId == personId && (c.OwnerUserId == null || c.OwnerUserId == userId)).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
					if (link == null) return (0, false, "There is no such contact.");
					isPrivate |= link.OwnerUserId != null;
					var person = await mRepository.GetQueryable<User>(u => u.Id == personId).AsNoTracking().Select(u => u.CompanyId).SingleOrDefaultAsync(cancellationToken);
					if (person is not int pc) return (0, false, "The contact has no company.");
					personCompany = pc;
				}
				if (companyId != null && companyId != personCompany) return (0, false, "The contact does not belong to that company.");
				companyId = personCompany;
			}

			if (companyId is not int cid) return (0, false, "Choose a company or a contact.");
			if (cid != ownerCompanyId)
			{
				var link = await mRepository.GetQueryable<CompanyContact>(c => c.OwnerCompanyId == ownerCompanyId && c.ContactCompanyId == cid && (c.OwnerUserId == null || c.OwnerUserId == userId)).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
				if (link == null) return (0, false, "There is no such company.");
				isPrivate |= link.OwnerUserId != null;
			}
			return (cid, isPrivate, null);
		}

		// ---- reading helpers ----

		private IQueryable<Activity> VisibleActivities(int ownerCompanyId, int userId) =>
			mRepository.GetQueryable<Activity>(a => a.OwnerCompanyId == ownerCompanyId && (a.OwnerUserId == null || a.OwnerUserId == userId));

		private async Task<ActivityDto> ToDtoAsync(Activity activity, CancellationToken cancellationToken)
		{
			var dto = (await ToDtosAsync<ActivityDto>(new List<Activity> { activity }, cancellationToken))[0];
			if (activity.SeriesId is int sid)
			{
				var series = await mRepository.GetQueryable<ActivitySeries>(s => s.Id == sid).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
				if (series != null) dto.Recurrence = new ActivityRecurrenceDto { FrequencyId = series.FrequencyId, Interval = series.Interval, EndAfterCount = series.EndAfterCount, EndOnDate = series.EndOnDate };
			}
			return dto;
		}

		private async Task<List<T>> ToDtosAsync<T>(List<Activity> activities, CancellationToken cancellationToken) where T : ActivityRowDto, new()
		{
			if (activities.Count == 0) return new();

			var companyIds = activities.Select(a => a.CompanyId).Distinct().ToList();
			var companies = await mRepository.GetQueryable<Company>(c => companyIds.Contains(c.Id)).AsNoTracking().Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken);
			var companyNames = companies.ToDictionary(c => c.Id, c => c.Name);

			var userIds = activities.SelectMany(a => new[] { a.ContactUserId, a.AssignedToUserId, a.CreatedByUserId }).Where(i => i != null).Select(i => i!.Value).Distinct().ToHashSet();
			var userNames = await UserNamesAsync(userIds, cancellationToken);

			// Which occurrence it is, and how many the series has (cancelled ones count).
			var seriesIds = activities.Where(a => a.SeriesId != null).Select(a => a.SeriesId!.Value).Distinct().ToList();
			var numbers = new Dictionary<int, (int Number, int Count)>();
			if (seriesIds.Count > 0)
			{
				var members = await mRepository.GetQueryable<Activity>(a => a.SeriesId != null && seriesIds.Contains(a.SeriesId.Value)).AsNoTracking()
					.Select(a => new { a.Id, a.SeriesId, a.StartAt }).ToListAsync(cancellationToken);
				foreach (var group in members.GroupBy(m => m.SeriesId))
				{
					var ordered = group.OrderBy(m => m.StartAt).ThenBy(m => m.Id).ToList();
					for (var i = 0; i < ordered.Count; i++) numbers[ordered[i].Id] = (i + 1, ordered.Count);
				}
			}

			return activities.Select(a =>
			{
				numbers.TryGetValue(a.Id, out var occurrence);
				return new T
				{
					Id = a.Id,
					TypeId = a.TypeId,
					Subject = a.Subject,
					Notes = a.Notes,
					PriorityId = a.PriorityId,
					StatusId = a.StatusId,
					CompanyId = a.CompanyId,
					CompanyName = companyNames.GetValueOrDefault(a.CompanyId),
					ContactUserId = a.ContactUserId,
					ContactName = a.ContactUserId is int c ? userNames.GetValueOrDefault(c) : null,
					AssignedToUserId = a.AssignedToUserId,
					AssignedToName = a.AssignedToUserId is int s ? userNames.GetValueOrDefault(s) : null,
					CreatedByUserId = a.CreatedByUserId,
					CreatedByName = a.CreatedByUserId is int u ? userNames.GetValueOrDefault(u) : null,
					StartAt = a.StartAt,
					DurationMinutes = a.DurationMinutes,
					CompletedAt = a.CompletedAt,
					SeriesId = a.SeriesId,
					OccurrenceNumber = a.SeriesId == null ? null : occurrence.Number,
					OccurrenceCount = a.SeriesId == null ? null : occurrence.Count,
					IsPrivate = a.OwnerUserId != null,
				};
			}).ToList();
		}

		/// <summary>The display names of users (contact people take their name from the contact info).</summary>
		private async Task<Dictionary<int, string>> UserNamesAsync(IEnumerable<int> ids, CancellationToken cancellationToken)
		{
			var list = ids.ToList();
			if (list.Count == 0) return new();
			var users = await mRepository.GetQueryable<User>(u => list.Contains(u.Id)).AsNoTracking().Select(u => new { u.Id, u.DisplayName, u.FirstName, u.LastName, u.UserName }).ToListAsync(cancellationToken);
			var infos = (await mRepository.GetQueryable<UserInfo>(i => list.Contains(i.UserId)).AsNoTracking().ToListAsync(cancellationToken)).ToDictionary(i => i.UserId);
			return users.ToDictionary(u => u.Id, u =>
			{
				infos.TryGetValue(u.Id, out var info);
				var fromInfo = $"{info?.FirstName} {info?.LastName}".Trim();
				return fromInfo.Length > 0 ? fromInfo : Pick(u.DisplayName, UserNameHelper.Compose(u.FirstName, u.LastName), u.UserName);
			});
		}

		/// <summary>The users who are active members of the company (its colleagues).</summary>
		private async Task<HashSet<int>> MemberIdsAsync(int companyId, CancellationToken cancellationToken) =>
			(await mRepository.GetQueryable<User2Company>(m => m.CompanyId == companyId && (m.StatusId == User2CompanyStatus.Active || m.StatusId == User2CompanyStatus.WithOtherCompany))
				.AsNoTracking().Select(m => m.UserId).ToListAsync(cancellationToken)).ToHashSet();

		private async Task TryRemoveSeriesAsync(int seriesId)
		{
			try
			{
				mRepository.DeleteAll<ActivitySeries>(s => s.Id == seriesId);
				await mRepository.SaveAsync();
			}
			catch
			{
				// The original failure is the one worth reporting.
			}
		}

		// ---- small helpers ----

		private static List<LookupItemDto> SortedByName(IEnumerable<LookupItemDto> items) =>
			items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

		private DateTime Now() => mTime.GetUtcNow().UtcDateTime;

		private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

		private static DateTime FromUnix(long seconds) => DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;

		private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

		private static string Pick(params string?[] names) => names.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "";

		private readonly IRepository mRepository;
		private readonly TimeProvider mTime;
	}
}
