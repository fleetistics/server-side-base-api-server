using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Notifications
{
	/// <summary>
	/// A pending delivery of a Notification to a user via one transport. Kept short-lived:
	/// removed once the transport-specific sender (e.g. the FCM sender) resolves it to a
	/// terminal NotificationSent row.
	/// </summary>
	public class NotificationQueue
	{
		public int Id { get; set; }
		public int NotificationId { get; set; }
		public Notification? Notification { get; set; }
		public byte TransportType { get; set; }
		public int UserId { get; set; }
		public DateTime Created { get; set; }
	}
}
