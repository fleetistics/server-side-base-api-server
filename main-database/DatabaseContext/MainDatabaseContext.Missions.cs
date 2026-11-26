using Microsoft.EntityFrameworkCore;

using db_model.Missions;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		// Ref tables are hand-filled like the other ref tables (no foreign key guards their ids), so they only get a key here.
		private void createMissionsModel(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<Mission>(entity =>
			{
				entity.ToTable("mission");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.Property(e => e.Name).IsRequired();
				entity.Property(e => e.JoinKey).IsRequired();
				// CompanyId null = a mission not bound to a company.
				entity.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
				entity.HasIndex(e => e.CompanyId);
				// "The user's active / creating mission" lookups in MissionService.
				entity.HasIndex(e => new { e.CreatorUserId, e.StatusId });
			});
			modelBuilder.Entity<MissionDetails>(entity =>
			{
				entity.ToTable("mission_details");
				entity.HasKey(e => e.MissionId);
				entity.Property(e => e.MissionId).ValueGeneratedNever();
				entity.HasOne<Mission>().WithOne().HasForeignKey<MissionDetails>(e => e.MissionId).OnDelete(DeleteBehavior.Cascade);
				entity.HasMany(e => e.Medias).WithOne().HasForeignKey(e => e.MissionId).OnDelete(DeleteBehavior.Cascade);
			});
			modelBuilder.Entity<MissionPrimaryTargetUser>(entity =>
			{
				entity.ToTable("mission_primary_target_user");
				entity.HasKey(e => e.MissionId);
				entity.Property(e => e.MissionId).ValueGeneratedNever();
				entity.HasOne<Mission>().WithOne().HasForeignKey<MissionPrimaryTargetUser>(e => e.MissionId).OnDelete(DeleteBehavior.Cascade);
			});
			modelBuilder.Entity<MissionUploadedMedia>(entity =>
			{
				entity.ToTable("mission_uploaded_media");
				entity.HasKey(e => new { e.MissionId, e.UploadedMediaId });
				entity.HasOne(e => e.Media).WithMany().HasForeignKey(e => e.UploadedMediaId);
			});
			modelBuilder.Entity<MissionStatus>(entity =>
			{
				entity.ToTable("mission_status");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedNever();
			});
			modelBuilder.Entity<MissionPrimaryTargetUserStatus>(entity =>
			{
				entity.ToTable("mission_primary_target_user_status");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedNever();
			});
		}
	}
}
