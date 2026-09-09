using Microsoft.EntityFrameworkCore;

using db_model.Translations;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createTranslationModel(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<Language>(entity =>
			{
				entity.ToTable("language");
				entity.HasKey(e => e.Code);
			});
			modelBuilder.Entity<TranslationToken>(entity =>
			{
				entity.ToTable("translation_token");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();

				entity.HasIndex(e => e.Text).IsUnique();
			});
			modelBuilder.Entity<Translation>(entity =>
			{
				entity.ToTable("translation");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();

				entity.HasIndex(e => new { e.TokenId, e.LanguageCode }).IsUnique();
				entity.HasOne(e => e.Token).WithMany().HasForeignKey(e => e.TokenId);
				entity.HasOne(e => e.Language).WithMany().HasForeignKey(e => e.LanguageCode);
			});
		}
	}
}
