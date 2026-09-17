using db_model.Notifications;
using Microsoft.EntityFrameworkCore;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createNotificationModel(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<NotificationType>(entity =>
			{
				entity.ToTable("notification_type");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
			});
			modelBuilder.Entity<Notification>(entity =>
			{
				entity.ToTable("notification");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
			});
			modelBuilder.Entity<NotificationToUser>(entity =>
			{
				entity.HasKey(e => new { e.UserId, e.NotificationId });
				entity.ToTable("notification2user");
				entity.HasOne(e => e.Notification).WithMany().HasForeignKey(e => e.NotificationId);
			});
			modelBuilder.Entity<NotificationTransportType>(entity =>
			{
				entity.ToTable("notification_transport_type");
				entity.HasKey(e => e.Id);
			});
			modelBuilder.Entity<NotificationResult>(entity =>
			{
				entity.ToTable("notification_result");
				entity.HasKey(e => e.Id);
			});
			modelBuilder.Entity<NotificationQueue>(entity =>
			{
				entity.ToTable("notification_queue");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.HasOne(e => e.Notification).WithMany().HasForeignKey(e => e.NotificationId);
			});
			modelBuilder.Entity<NotificationSent>(entity =>
			{
				entity.ToTable("notification_sent");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.HasOne(e => e.Notification).WithMany().HasForeignKey(e => e.NotificationId);
			});
			modelBuilder.Entity<FCMNotificationSent>(entity =>
			{
				entity.ToTable("fcm_notification_sent");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.HasOne(e => e.NotificationSent).WithMany().HasForeignKey(e => e.NotificationSentId);
			});
		}
	}
}
