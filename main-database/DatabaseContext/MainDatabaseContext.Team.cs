using Microsoft.EntityFrameworkCore;

using db_model.Team;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createTeamModel(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<Team>(entity =>
			{
				entity.ToTable("team");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
			});
			modelBuilder.Entity<TeamDetails>(entity =>
			{
				entity.ToTable("team_details");
				entity.HasKey(e => e.TeamId);
				entity.Property(e => e.TeamId).ValueGeneratedNever();
				entity.HasOne<Team>().WithOne().HasForeignKey<TeamDetails>(e => e.TeamId);
			});
			modelBuilder.Entity<TeamMemberUser>(entity =>
			{
				entity.ToTable("team_member_user");
				entity.HasKey(e => new { e.TeamId, e.UserId });
				entity.HasOne<Team>().WithMany(t => t.Members).HasForeignKey(e => e.TeamId);
			});
			modelBuilder.Entity<TeamUploadedMedia>(entity =>
			{
				entity.ToTable("team_uploaded_media");
				entity.HasKey(e => new { e.TeamId, e.UploadedMediaId });
				entity.HasOne<Team>().WithMany(e => e.UploadedMedias).HasForeignKey(e => e.TeamId);
				entity.HasOne(e => e.Media).WithMany().HasForeignKey(e => e.UploadedMediaId);
			});
			modelBuilder.Entity<TeamStatus>(entity =>
			{
				entity.ToTable("team_status");
				entity.HasKey(e => e.Id);
			});
			modelBuilder.Entity<TeamMemberUserStatus>(entity =>
			{
				entity.ToTable("team_user_status");
				entity.HasKey(e => e.Id);
			});
		}
	}
}
