using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace mf.aiApi.mainDatabase.DatabaseContext
{
	// Any table with a "LatestUpdate" column gets its update trigger for free: no
	// migration needs to hand-write CREATE TRIGGER. The shared update_latestupdate_column()
	// function itself is created once, by hand, in the first migration that needs it.
	public sealed class LatestUpdateTriggerSqlGenerator : NpgsqlMigrationsSqlGenerator
	{
		private const string LatestUpdateColumn = "LatestUpdate";

		public LatestUpdateTriggerSqlGenerator(
			MigrationsSqlGeneratorDependencies dependencies,
			INpgsqlSingletonOptions npgsqlOptions)
			: base(dependencies, npgsqlOptions)
		{
		}

		protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
		{
			var addsTrigger = operation.Columns.Any(c => c.Name == LatestUpdateColumn);

			base.Generate(operation, model, builder, terminate: !addsTrigger && terminate);

			if (addsTrigger)
			{
				AppendCreateTrigger(builder, operation.Name, terminate);
			}
		}

		protected override void Generate(AddColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
		{
			var addsTrigger = operation.Name == LatestUpdateColumn;

			base.Generate(operation, model, builder, terminate: !addsTrigger && terminate);

			if (addsTrigger)
			{
				AppendCreateTrigger(builder, operation.Table, terminate);
			}
		}

		private void AppendCreateTrigger(MigrationCommandListBuilder builder, string tableName, bool terminate)
		{
			// The base call above was told not to terminate its own statement, since we're
			// appending another one to the same batch - add the separator ourselves first.
			builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);

			builder
				.AppendLine($"CREATE OR REPLACE TRIGGER trg_{tableName}_latest_update")
				.AppendLine($"BEFORE UPDATE ON \"{tableName}\"")
				.AppendLine("FOR EACH ROW EXECUTE FUNCTION update_latestupdate_column();");

			if (terminate)
			{
				builder.EndCommand();
			}
		}
	}
}
