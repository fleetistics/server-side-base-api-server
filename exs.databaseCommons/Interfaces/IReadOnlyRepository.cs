using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace exs.Database.Commons.Interfaces
{
    public interface IReadOnlyRepository : IDisposable
    {
        DbContext DbContext { get; }

        Type? GetEntityType(string entityName);

        IQueryable<TEntity> GetQueryable<TEntity>(Expression<Func<TEntity, bool>>? filter = null) where TEntity : class;
        IQueryable<object> GetQueryable(Type entityType);
        IQueryable<object> GetQueryable(string entityTypeName);
        
        TEntity? GetById<TEntity>(object id) where TEntity : class;
        ValueTask<TEntity?> GetByIdAsync<TEntity>(object id, CancellationToken cancellationToken = default) where TEntity : class;
        object? GetById(Type entityType, object id);

        ValueTask<object?> GetByIdAsync(Type entityType, object id, CancellationToken cancellationToken = default);
    }
}
