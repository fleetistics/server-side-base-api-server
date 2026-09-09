using db_model.Log;
using Microsoft.EntityFrameworkCore;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	public partial class MainDatabaseContext
	{
		private void createLogModel(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<ClientLogRecord>(entity =>
			{
				entity.ToTable("client_log_record");
				entity.HasKey(e => new { e.Id });
				entity.Property(e => e.Id).ValueGeneratedOnAdd();
			});
		}
	}
}
