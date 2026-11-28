using System.ComponentModel.DataAnnotations;

namespace api_server.Controllers.Contacts.Dto
{
	/// <summary>The lists the activity forms pick from. Every list is sorted by name.</summary>
	public class ActivityLookupsDto
	{
		/// <summary>1 Call Complete, 2 Call Attempt, 3 Call VM, 4 Meeting, 5 Email, 6 Site Visit, 7 Webinar, 8 Other.</summary>
		public List<LookupItemDto> Types { get; set; } = new();
		/// <summary>1 Low, 2 Normal, 3 High.</summary>
		public List<LookupItemDto> Priorities { get; set; } = new();
		/// <summary>1 Scheduled, 2 Completed, 3 Cancelled.</summary>
		public List<LookupItemDto> Statuses { get; set; } = new();
		/// <summary>1 Daily, 2 Weekly, 3 Monthly, 4 Yearly.</summary>
		public List<LookupItemDto> Frequencies { get; set; } = new();
		/// <summary>The people an activity can be assigned to: the members of the caller's company.</summary>
		public List<UserLookupDto> Users { get; set; } = new();
	}

	/// <summary>How a recurring activity repeats (as the form sends it, and as it is read back).</summary>
	public class ActivityRecurrenceDto
	{
		/// <summary>A RecurrenceFrequency id.</summary>
		public short FrequencyId { get; set; }
		/// <summary>Every this many days / weeks / months / years (1 to 99).</summary>
		public int Interval { get; set; } = 1;
		/// <summary>Ends after this many occurrences (2 to 100, the first one included). Send this or <see cref="EndOnDate"/>.</summary>
		public int? EndAfterCount { get; set; }
		/// <summary>Ends on this day (no occurrence after it). At most 100 occurrences are made.</summary>
		public DateOnly? EndOnDate { get; set; }
	}

	/// <summary>One activity in a list.</summary>
	public class ActivityRowDto
	{
		public int Id { get; set; }
		public short TypeId { get; set; }
		public string Subject { get; set; } = default!;
		public string? Notes { get; set; }
		public short PriorityId { get; set; }
		public short StatusId { get; set; }
		public int? CompanyId { get; set; }
		public string? CompanyName { get; set; }
		public int? ContactUserId { get; set; }
		public string? ContactName { get; set; }
		public int? AssignedToUserId { get; set; }
		public string? AssignedToName { get; set; }
		public int? CreatedByUserId { get; set; }
		public string? CreatedByName { get; set; }
		/// <summary>When it starts (or started).</summary>
		public DateTime StartAt { get; set; }
		public int DurationMinutes { get; set; }
		public DateTime? CompletedAt { get; set; }
		/// <summary>Set when this is one occurrence of a recurring activity.</summary>
		public int? SeriesId { get; set; }
		/// <summary>Which occurrence this is (1 for the first) and how many the series has; both null when it is not recurring.</summary>
		public int? OccurrenceNumber { get; set; }
		public int? OccurrenceCount { get; set; }
		public bool IsPrivate { get; set; }
	}

	/// <summary>One activity with the rule of its series.</summary>
	public class ActivityDto : ActivityRowDto
	{
		public ActivityRecurrenceDto? Recurrence { get; set; }
	}

	/// <summary>What the activity form sends to log or schedule an activity, and to edit it.</summary>
	public class SaveActivityDto
	{
		/// <summary>An ActivityType id.</summary>
		public short TypeId { get; set; }
		[MaxLength(200)] public string Subject { get; set; } = "";
		[MaxLength(8000)] public string? Notes { get; set; }
		/// <summary>An ActivityPriority id; 2 Normal when not sent.</summary>
		public short? PriorityId { get; set; }
		/// <summary>The account it is about. Optional when a contact is given (it is then the contact's company).</summary>
		public int? CompanyId { get; set; }
		/// <summary>The person it is about (a contact or a colleague), when there is one.</summary>
		public int? ContactUserId { get; set; }
		/// <summary>Defaults to the caller.</summary>
		public int? AssignedToUserId { get; set; }
		/// <summary>When it starts (or started), a Unix time in seconds.</summary>
		public DateTime StartAt { get; set; }
		/// <summary>In minutes, 0 to 1440.</summary>
		public int DurationMinutes { get; set; } = 30;
		/// <summary>Create only. True: it already happened, so it is saved as Completed. False: it is Scheduled.</summary>
		public bool Log { get; set; }
		/// <summary>Create only, Scheduled only: makes the series of occurrences. Not sent on an edit (the rule cannot change).</summary>
		public ActivityRecurrenceDto? Recurrence { get; set; }
		/// <summary>Create only: only the caller sees it. It is also private when its company or contact is.</summary>
		public bool IsPrivate { get; set; }
	}
}
