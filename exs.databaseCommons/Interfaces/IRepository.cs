using System.Linq.Expressions;

namespace exs.Database.Commons.Interfaces
{
    public interface IRepository : IReadOnlyRepository
    {
        void Create<TEntity>(TEntity entity) where TEntity : class;
        void Update<TEntity>(TEntity entity) where TEntity : class;
        void Attach<TEntity>(TEntity entity) where TEntity : class;
        void Detach<TEntity>(TEntity entity) where TEntity : class;
        void Delete<TEntity>(object id) where TEntity : class;
        void Delete<TEntity>(TEntity entity) where TEntity : class;
        void DeleteAll<TEntity>(Expression<Func<TEntity, bool>> filter) where TEntity : class;
        void DeleteAll<TEntity>(IEnumerable<TEntity> list) where TEntity : class;

        int Save();
        Task<int> SaveAsync(CancellationToken cancellationToken = default);
    }
}
