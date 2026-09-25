namespace db_model.Notifications
{
	/// <summary>
	/// One row per mobile UserSession the FCM sender actually attempted for a NotificationSent
	/// (a user can have several active sessions/devices at once). SessionId and FCMToken are
	/// historical snapshots taken at send time - the session may since have logged out, rotated,
	/// or been deleted.
	/// </summary>
	public class FCMNotificationSent
	{
		public int Id { get; set; }
		public int NotificationSentId { get; set; }
		public NotificationSent? NotificationSent { get; set; }
		public int SessionId { get; set; }
		public string FCMToken { get; set; } = default!;
		public byte AttemptNumber { get; set; }
		public byte Result { get; set; }
		public string? ErrorCode { get; set; }
		public string? ProviderMessageId { get; set; }
		public DateTime SentAt { get; set; }
	}
}
