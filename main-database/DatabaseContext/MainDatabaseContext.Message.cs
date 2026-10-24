using Microsoft.EntityFrameworkCore;

using db_model.Messages;
using db_model.Team;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createMessageModel(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<UserMessage>(entity =>
			{
				entity.ToTable("user_message");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.HasMany(e => e.UserMessage2Users).WithOne().HasForeignKey(e => e.UserMessageId);
			});
			modelBuilder.Entity<UserMessage2User>(entity =>
			{
				entity.ToTable("user_message2user");
				entity.HasKey(e => new { e.UserMessageId, e.UserId });
			});
			modelBuilder.Entity<UserMessageStatus>(entity =>
			{
				entity.ToTable("user_message_status");
				entity.HasKey(e => e.Id);
			});
			modelBuilder.Entity<UserMessage2UserStatus>(entity =>
			{
				entity.ToTable("user_message2user_status");
				entity.HasKey(e => e.Id);
			});

			modelBuilder.Entity<TeamActivity>(entity =>
			{
				entity.ToTable("team_activity");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.HasMany(e => e.TeamActivity2Users).WithOne().HasForeignKey(e => e.TeamActivityId);
			});
			modelBuilder.Entity<TeamActivity2User>(entity =>
			{
				entity.ToTable("team_activity2user");
				entity.HasKey(e => new { e.TeamActivityId, e.UserId });
			});
			modelBuilder.Entity<TeamActivityType>(entity =>
			{
				entity.ToTable("team_activity_type");
				entity.HasKey(e => e.Id);
			});
		}
	}
}
