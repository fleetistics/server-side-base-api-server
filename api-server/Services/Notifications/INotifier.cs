using db_model.Notifications;

namespace api_server.Services.Notifications
{
	public interface INotifier
	{
		Task QueueNotificationAsync(Notification notification, IEnumerable<int> userIds, CancellationToken cancellationToken);
	}
}
