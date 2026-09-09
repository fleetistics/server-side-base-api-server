using exs.Database.Commons.Impl;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using System.Linq.Expressions;

namespace exs.Database.Commons.Impl
{
    public static partial class CustomExtensions
    {
        public static IQueryable Query(this DbContext context, string entityName) =>
            context.Query(context.Model.FindEntityType(entityName)?.ClrType ??
                throw new ArgumentException("Unknown type: " + entityName, nameof(entityName)));

#pragma warning disable EF1001 // Internal EF Core API usage.
        public static IQueryable Query(this DbContext context, Type entityType) =>
            (IQueryable)((IDbSetCache)context).GetOrAddSet(context.GetDependencies()?.SetSource ??
                throw new ArgumentException("Unknown type: " + entityType, nameof(entityType)), entityType);
#pragma warning restore EF1001 // Internal EF Core API usage.
    }
}

namespace exs.Database
{
    public class EntityFrameworkReadOnlyRepository<TContext> : IReadOnlyRepository where TContext : DbContext
    {
        protected readonly TContext mContext;

        public EntityFrameworkReadOnlyRepository(TContext context)
        {
            mContext = context;
        }

        public DbContext DbContext => mContext;

        public Type? GetEntityType(string entityName)
        {
            foreach (var t in mContext.Model.GetEntityTypes().Select(e => e.ClrType))
            {
                if (string.Equals(t.Name, entityName, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
            return null;
        }

        public IQueryable<TEntity> GetQueryable<TEntity>(Expression<Func<TEntity, bool>>? filter = null) where TEntity : class
        {
            IQueryable<TEntity> query = mContext.Set<TEntity>();
            if (filter != null)
            {
                query = query.Where(filter);
            }
            return query;
        }

        public IQueryable<object> GetQueryable(Type entityType)
        {
            return (IQueryable<object>)mContext.Query(mContext.Model.FindEntityType(entityType)?.ClrType ??
                throw new ArgumentException("Unknown type: " + entityType, nameof(entityType)));
        }

        public IQueryable<object> GetQueryable(string entityTypeName)
        {
            return (IQueryable<object>)mContext.Query(mContext.Model.FindEntityType(entityTypeName)?.ClrType ??
                throw new ArgumentException("Unknown type: " + entityTypeName, nameof(entityTypeName)));
        }

        public TEntity? GetById<TEntity>(object id) where TEntity : class
        {
            return mContext.Set<TEntity>().Find(new object[] { id });
        }

        public ValueTask<TEntity?> GetByIdAsync<TEntity>(object id, CancellationToken cancellationToken = default) where TEntity : class
        {
            return mContext.Set<TEntity>().FindAsync(new object[] { id }, cancellationToken);
        }

        public object? GetById(Type entityType, object id)
        {
            return mContext.Find(entityType, new object[] { id });
        }

        public ValueTask<object?> GetByIdAsync(Type entityType, object id, CancellationToken cancellationToken = default)
        {
            return mContext.FindAsync(entityType, new object[] { id }, cancellationToken);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    //mContext.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~EntityFrameworkReadOnlyRepository() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
