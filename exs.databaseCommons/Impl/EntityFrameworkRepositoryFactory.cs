using exs.Database.Commons.Interfaces;
using exs.databaseCommons.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace exs.databaseCommons.Impl
{
	public class EntityFrameworkRepositoryFactory<R> : IRepositoryFactory<R>, IDisposable where R : IRepository
	{
		public EntityFrameworkRepositoryFactory(IServiceProvider scopeFactory)
		{
			mScope = scopeFactory.CreateScope();
		}

		public IRepository CreateDatabaseRepository()
		{
			return mScope.ServiceProvider.GetRequiredService<R>();
		}

		public void Dispose()
		{
			mScope.Dispose();
		}

		private readonly IServiceScope mScope;
	}

	public class EntityFrameworkRepositoryFactory : EntityFrameworkRepositoryFactory<IScopedRepository>, IRepositoryFactory
	{
		public EntityFrameworkRepositoryFactory(IServiceProvider scopeFactory) : base(scopeFactory)
		{
		}
	}
}
