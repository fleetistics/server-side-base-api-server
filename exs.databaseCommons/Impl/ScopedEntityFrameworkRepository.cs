using exs.Database.Commons.Impl;
using exs.databaseCommons.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace exs.databaseCommons.Impl
{
	public class ScopedEntityFrameworkRepository<TContext> : EntityFrameworkRepository<TContext>, IScopedRepository where TContext : Microsoft.EntityFrameworkCore.DbContext
	{
		public ScopedEntityFrameworkRepository(TContext context, IServiceScope serviceScope) : base(context)
		{
			mCurrentScope = serviceScope;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
			{
				mCurrentScope.Dispose();
			}
		}

		protected readonly IServiceScope mCurrentScope;
	}
}
