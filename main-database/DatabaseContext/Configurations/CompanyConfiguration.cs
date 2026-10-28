using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using db_model.Companies;

namespace mf.aiApi.mainDatabase.DatabaseContext.Configurations
{
	public class CompanyConfiguration : IEntityTypeConfiguration<Company>
	{
		public void Configure(EntityTypeBuilder<Company> entity)
		{
			entity.ToTable("company");
			entity.HasKey(e => new { e.Id });
			entity.Property(e => e.Id).ValueGeneratedOnAdd();
		}
	}
}
