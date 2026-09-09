using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace exs.Database.Commons.Impl
{
    public class EntityFrameworkRepository<TContext> : EntityFrameworkReadOnlyRepository<TContext>, IRepository where TContext : DbContext
    {
        public EntityFrameworkRepository(TContext context) : base(context)
        {
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Create<TEntity>(TEntity entity) where TEntity : class
        {
            if (!mContext.Set<TEntity>().Local.Contains(entity))
            {
                mContext.Set<TEntity>().Add(entity);
            }
        }

        public void Update<TEntity>(TEntity entity) where TEntity : class
        {
            if (!mContext.Set<TEntity>().Local.Contains(entity))
            {
                mContext.Set<TEntity>().Attach(entity);
                mContext.Entry(entity).State = EntityState.Modified;
            }
        }

        public void Attach<TEntity>(TEntity entity) where TEntity : class
        {
            if (!mContext.Set<TEntity>().Local.Contains(entity))
            {
                mContext.Set<TEntity>().Update(entity);
                mContext.Entry(entity).State = EntityState.Modified;
            }
        }

        public void Detach<TEntity>(TEntity entity) where TEntity : class
        {
            if (mContext.Set<TEntity>().Local.Contains(entity))
            {
                mContext.Entry(entity).State = EntityState.Detached;
            }
        }

        public void Delete<TEntity>(object id) where TEntity : class
        {
            var entity = mContext.Set<TEntity>().Find(id);
            if (entity != null) Delete(entity);
        }

        public void Delete<TEntity>(TEntity entity) where TEntity : class
        {
            var dbSet = mContext.Set<TEntity>();
            if (mContext.Entry(entity).State == EntityState.Detached)
            {
                dbSet.Attach(entity);
            }
            dbSet.Remove(entity);
        }

        public void DeleteAll<TEntity>(Expression<Func<TEntity, bool>> filter) where TEntity : class
        {
            var dbSet = mContext.Set<TEntity>();
            dbSet.RemoveRange(dbSet.Where(filter));
        }

        public void DeleteAll<TEntity>(IEnumerable<TEntity> list) where TEntity : class
        {
            mContext.Set<TEntity>().RemoveRange(list);
        }

        public Task<int> SaveAsync(CancellationToken cancellationToken = default)
        {
            return mContext.SaveChangesAsync(cancellationToken);
        }

        public int Save()
        {
            return mContext.SaveChanges();
        }
    }
}
