namespace db_model.Notifications
{
	/// <summary>
	/// Terminal record for a NotificationQueue item: written once every session attempted for
	/// this (Notification, User, TransportType) has a final outcome. Result is Ok as soon as at
	/// least one FCMNotificationSent (or future per-transport) attempt succeeded.
	/// </summary>
	public class NotificationSent
	{
		public int Id { get; set; }
		public int NotificationId { get; set; }
		public Notification? Notification { get; set; }
		public byte TransportType { get; set; }
		public int UserId { get; set; }
		public DateTime Created { get; set; }
		public DateTime SentAt { get; set; }
		public byte Result { get; set; }
	}
}
