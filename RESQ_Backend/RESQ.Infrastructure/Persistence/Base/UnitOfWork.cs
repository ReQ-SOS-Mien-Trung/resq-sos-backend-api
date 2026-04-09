using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Base
{

    public class UnitOfWork(ResQDbContext context, ILogger<UnitOfWork> logger) : IUnitOfWork
    {
        private readonly ResQDbContext _context = context;
        private readonly ILogger<UnitOfWork> _logger = logger;

        public IGenericRepository<T> GetRepository<T>() where T : class
        {
            return new GenericRepository<T>(_context);
        }

        public IQueryable<T> Set<T>() where T : class
        {
            return _context.Set<T>().AsNoTracking();
        }

        public IQueryable<T> SetTracked<T>() where T : class
        {
            return _context.Set<T>();
        }

        public int SaveChangesWithTransaction()
        {
            int result = -1;

            //System.Data.IsolationLevel.Snapshot
            using (var dbContextTransaction = _context.Database.BeginTransaction())
            {
                try
                {
                    result = _context.SaveChanges();
                    dbContextTransaction.Commit();
                }
                catch (Exception)
                {
                    //Log Exception Handling message                      
                    result = -1;
                    dbContextTransaction.Rollback();
                }
            }

            return result;
        }

        public async Task<int> SaveAsync()
        {
            try
            {
                return await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var entries = ex.Entries.Select(e =>
                {
                    var pk = e.Metadata.FindPrimaryKey();
                    var keys = pk != null ? string.Join(", ", pk.Properties.Select(p => e.CurrentValues[p])) : "unknown";
                    return $"{e.Entity.GetType().Name} (Key: {keys})";
                });
                _logger.LogWarning("Concurrency conflict on SaveAsync. Affected entries: {Entries}", string.Join("; ", entries));
                throw new ConflictException("Dữ liệu tồn kho đã thay đổi bởi thao tác khác. Vui lòng thử lại.");
            }
        }

        public async Task<int> SaveChangesWithTransactionAsync()
        {
            int result = -1;

            //System.Data.IsolationLevel.Snapshot
            using (var dbContextTransaction = _context.Database.BeginTransaction())
            {
                try
                {
                    result = await _context.SaveChangesAsync();
                    dbContextTransaction.Commit();
                }
                catch (Exception)
                {
                    //Log Exception Handling message                      
                    result = -1;
                    dbContextTransaction.Rollback();
                }
            }

            return result;
        }

        public void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class
        {
            _context.Entry(entity).State = EntityState.Unchanged;
        }

        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await action();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
