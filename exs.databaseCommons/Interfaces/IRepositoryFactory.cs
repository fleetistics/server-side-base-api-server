namespace exs.Database.Commons.Interfaces
{
    public interface IRepositoryFactory<R> where R : IRepository
    {
        IRepository CreateDatabaseRepository();
    }

	public interface IRepositoryFactory : IRepositoryFactory<IRepository>
	{
	}
}
