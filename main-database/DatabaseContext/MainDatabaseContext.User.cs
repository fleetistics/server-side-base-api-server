using Microsoft.EntityFrameworkCore;

using db_model.UserManagement;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createUserModel(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<User>(entity =>
			{
				entity.ToTable("user");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();

				entity.HasOne(e => e.AvatarImage).WithMany().HasForeignKey(e => e.AvatarImageId);
            });
            modelBuilder.Entity<UserStatus>(entity =>
            {
                entity.ToTable("user_status");
                entity.HasKey(e => new { e.Id });
            });
            modelBuilder.Entity<UserSource>(entity =>
            {
                entity.ToTable("user_source");
                entity.HasKey(e => new { e.Id });
            });
            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.ToTable("user_session");
                entity.HasKey(e => new { e.Id });
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.ComplexProperty(e => e.Token);
                entity.ComplexProperty(e => e.ClientInfo);
                entity.HasOne(e => e.MobileGpsDevice).WithMany().HasForeignKey(e => e.MobileGpsDeviceId).IsRequired(false);
                //entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            });
            modelBuilder.Entity<UserSessionStatus>(entity =>
            {
                entity.ToTable("user_session_status");
                entity.HasKey(e => new { e.Id });
            });
            modelBuilder.Entity<UserSessionSource>(entity =>
            {
                entity.ToTable("user_session_source");
                entity.HasKey(e => new { e.Id });
            });
            modelBuilder.Entity<UserLocationPrivacy>(entity =>
            {
                entity.ToTable("user_location_privacy");
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).ValueGeneratedNever();
            });
            modelBuilder.Entity<UserSettings>(entity =>
            {
                entity.ToTable("user_settings");
                entity.HasKey(e => new { e.Id });
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
            });
            modelBuilder.Entity<UserEmergencyAlertStatus>(entity =>
            {
                entity.ToTable("user_emergency_alert_status");
                entity.HasKey(e => new { e.Id });
            });
            modelBuilder.Entity<UserEmergencyAlert>(entity =>
            {
                entity.ToTable("user_emergency_alert");
                entity.HasKey(e => new { e.Id });
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
            });
        }
	}
}
