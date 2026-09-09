using db_model.Media;
using Microsoft.EntityFrameworkCore;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createMediaModel(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<UploadedMedia>(entity =>
			{
				entity.ToTable("uploaded_media");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
				// Backs the invariant MediaStorageService/MediaInboundProcessor rely on
				// when checking for an existing row by Guid before inserting — without this,
				// the check-then-insert is only as safe as every caller remembering to take
				// UploadedMediaTableLock first. Excludes '' so the pre-this-column legacy
				// rows backfilled with an empty Guid don't collide with each other.
				entity.HasIndex(e => e.Guid).IsUnique().HasFilter("\"Guid\" <> ''");
			});
		}
	}
}
