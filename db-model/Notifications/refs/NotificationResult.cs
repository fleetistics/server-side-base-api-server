namespace db_model.Notifications
{
	/// <summary>
	/// Final delivery outcome for a NotificationSent/FCMNotificationSent row.
	/// A NotificationSent is Ok as soon as at least one FCMNotificationSent attempt for it is Ok.
	/// </summary>
	public class NotificationResult
	{
		public const byte Ok = 1;
		public const byte Error = 2;

		public byte Id { get; set; }
		public string Name { get; set; } = default!;
		public DateTime LatestUpdate { get; set; }
	}
}
