#pragma warning disable EF1001 // MigrationsModelDiffer is "internal infrastructure" but is the documented extension point for this.

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update.Internal;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	// `dotnet ef migrations add` must never script a DROP TABLE/COLUMN: entities and
	// properties get removed from the model over time, but production data underneath
	// a retired column/table stays put until someone drops it by hand. Filtering here
	// (rather than in the SQL generator) keeps the operations out of the migration file
	// entirely, so the model snapshot and future diffs never see them again either.
	public sealed class NoDropMigrationsModelDiffer : MigrationsModelDiffer
	{
		public NoDropMigrationsModelDiffer(
			IRelationalTypeMappingSource typeMappingSource,
			IMigrationsAnnotationProvider migrationsAnnotationProvider,
			IRelationalAnnotationProvider relationalAnnotationProvider,
			IRowIdentityMapFactory rowIdentityMapFactory,
			CommandBatchPreparerDependencies commandBatchPreparerDependencies)
			: base(typeMappingSource, migrationsAnnotationProvider, relationalAnnotationProvider, rowIdentityMapFactory, commandBatchPreparerDependencies)
		{
		}

		public override IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
		{
			return base.GetDifferences(source, target)
				.Where(op => op is not DropTableOperation && op is not DropColumnOperation)
				.ToList();
		}
	}
}
