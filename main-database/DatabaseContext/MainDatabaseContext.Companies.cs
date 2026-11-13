using Microsoft.EntityFrameworkCore;

using db_model.Companies;
using db_model.UserManagement;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		// The company table itself is configured in createFleetModel (CompanyConfiguration, shared with the feeders' context).
		// Ref tables are hand-filled like the other ref tables (no foreign key guards their ids), so they only get a key here.
		private void createCompaniesModel(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<CompanyStatus>(entity =>
			{
				entity.ToTable("company_status");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedNever();
			});
			modelBuilder.Entity<User2CompanyStatus>(entity =>
			{
				entity.ToTable("user2company_status");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedNever();
			});
			modelBuilder.Entity<User2CompanyJoinType>(entity =>
			{
				entity.ToTable("user2company_join_type");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedNever();
			});
			modelBuilder.Entity<UserJoinInvitationStatus>(entity =>
			{
				entity.ToTable("user_join_invitation_status");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedNever();
			});
			modelBuilder.Entity<UserJoinRequestStatus>(entity =>
			{
				entity.ToTable("user_join_request_status");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedNever();
			});

			modelBuilder.Entity<PermissionRole>(entity =>
			{
				entity.ToTable("permission_role");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.Property(e => e.Name).IsRequired();
				// CompanyId null = a system-wide role every company can use.
				entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
				entity.HasIndex(e => e.CompanyId);
			});

			modelBuilder.Entity<UserGroup>(entity =>
			{
				entity.ToTable("user_group");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.Property(e => e.Name).IsRequired();
				entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
				entity.HasIndex(e => e.CompanyId);
			});

			modelBuilder.Entity<User2Company>(entity =>
			{
				entity.ToTable("user2company");
				entity.HasKey(e => new { e.CompanyId, e.UserId });
				entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
				entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
				// A role in use cannot be deleted from under its members.
				entity.HasOne<PermissionRole>().WithMany().HasForeignKey(e => e.PermissionRoleId).OnDelete(DeleteBehavior.Restrict);
				entity.HasIndex(e => e.UserId);
				entity.HasIndex(e => e.PermissionRoleId);
			});

			modelBuilder.Entity<User2Group>(entity =>
			{
				entity.ToTable("user2group");
				entity.HasKey(e => new { e.GroupId, e.UserId });
				entity.HasOne<UserGroup>().WithMany().HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Cascade);
				entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
				// CompanyId repeats the group's company so a company's memberships are one indexed read; the FK goes to the
				// company only (NoAction avoids a second cascade path through user_group).
				entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.NoAction);
				entity.HasIndex(e => new { e.CompanyId, e.UserId });
				entity.HasIndex(e => e.UserId);
			});

			modelBuilder.Entity<UserJoinInvitation>(entity =>
			{
				entity.ToTable("user_join_invitation");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.Property(e => e.JoinCode).IsRequired();
				entity.Property(e => e.JoinQrCodeImageName).IsRequired();
				// The code is how an invitation is looked up when a user joins.
				entity.HasIndex(e => e.JoinCode).IsUnique();
				entity.HasIndex(e => e.CompanyId);
				entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
				entity.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
				// Optional presets: losing the role/group/invited user must not delete the invitation.
				entity.HasOne<PermissionRole>().WithMany().HasForeignKey(e => e.PermissionRoleId).OnDelete(DeleteBehavior.SetNull);
				entity.HasOne<UserGroup>().WithMany().HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.SetNull);
				entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
			});

			modelBuilder.Entity<UserJoinRequest>(entity =>
			{
				entity.ToTable("user_join_request");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
				entity.HasOne<UserJoinInvitation>().WithMany().HasForeignKey(e => e.UserJoinInvitationId).OnDelete(DeleteBehavior.Cascade);
				entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
				entity.HasIndex(e => new { e.CompanyId, e.StatusId });
				entity.HasIndex(e => e.UserId);
				entity.HasIndex(e => e.UserJoinInvitationId);
			});
		}
	}
}
