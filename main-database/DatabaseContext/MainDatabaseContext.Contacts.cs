using Microsoft.EntityFrameworkCore;

using db_model.Companies;
using db_model.Contacts;
using db_model.UserManagement;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		// The CRM side of companies and users: extra facts (company_info, user_info, company_location) and who owns which contact
		// (company_contact, company_user_contact). Ref tables are hand-filled like the other ref tables, so they only get a key here.
		private void createContactsModel(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<CompanyType>(entity => { entity.ToTable("company_type"); entity.HasKey(e => e.Id); entity.Property(e => e.Id).ValueGeneratedNever(); });
			modelBuilder.Entity<ContactType>(entity => { entity.ToTable("contact_type"); entity.HasKey(e => e.Id); entity.Property(e => e.Id).ValueGeneratedNever(); });
			modelBuilder.Entity<LeadSource>(entity => { entity.ToTable("lead_source"); entity.HasKey(e => e.Id); entity.Property(e => e.Id).ValueGeneratedNever(); });
			modelBuilder.Entity<PaymentTerm>(entity => { entity.ToTable("payment_term"); entity.HasKey(e => e.Id); entity.Property(e => e.Id).ValueGeneratedNever(); });

			modelBuilder.Entity<CompanyInfo>(entity =>
			{
				entity.ToTable("company_info");
				entity.HasKey(e => e.CompanyId);
				entity.Property(e => e.CompanyId).ValueGeneratedNever();
				entity.Property(e => e.AnnualRevenue).HasPrecision(18, 2);
				entity.HasOne<Company>().WithOne().HasForeignKey<CompanyInfo>(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
				// Losing the account manager or the primary contact must not delete the company's info.
				entity.HasOne<User>().WithMany().HasForeignKey(e => e.AccountManagerUserId).OnDelete(DeleteBehavior.SetNull);
				entity.HasOne<User>().WithMany().HasForeignKey(e => e.PrimaryContactUserId).OnDelete(DeleteBehavior.SetNull);
			});

			modelBuilder.Entity<CompanyLocation>(entity =>
			{
				entity.ToTable("company_location");
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				entity.HasIndex(e => e.CompanyId);
				entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
			});

			modelBuilder.Entity<UserInfo>(entity =>
			{
				entity.ToTable("user_info");
				entity.HasKey(e => e.UserId);
				entity.Property(e => e.UserId).ValueGeneratedNever();
				entity.HasOne<User>().WithOne().HasForeignKey<UserInfo>(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
			});

			modelBuilder.Entity<CompanyContact>(entity =>
			{
				entity.ToTable("company_contact");
				entity.HasKey(e => new { e.OwnerCompanyId, e.ContactCompanyId });
				// A contact company belongs to one owner only.
				entity.HasIndex(e => e.ContactCompanyId).IsUnique();
				entity.HasIndex(e => new { e.OwnerCompanyId, e.OwnerUserId });
				entity.HasOne<Company>().WithMany().HasForeignKey(e => e.OwnerCompanyId).OnDelete(DeleteBehavior.Cascade);
				entity.HasOne<Company>().WithMany().HasForeignKey(e => e.ContactCompanyId).OnDelete(DeleteBehavior.Cascade);
				entity.HasOne<User>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
			});

			modelBuilder.Entity<CompanyUserContact>(entity =>
			{
				entity.ToTable("company_user_contact");
				entity.HasKey(e => new { e.OwnerCompanyId, e.ContactUserId });
				// A contact person belongs to one owner only.
				entity.HasIndex(e => e.ContactUserId).IsUnique();
				entity.HasIndex(e => new { e.OwnerCompanyId, e.OwnerUserId });
				entity.HasOne<Company>().WithMany().HasForeignKey(e => e.OwnerCompanyId).OnDelete(DeleteBehavior.Cascade);
				entity.HasOne<User>().WithMany().HasForeignKey(e => e.ContactUserId).OnDelete(DeleteBehavior.Cascade);
				entity.HasOne<User>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
			});
		}
	}
}
