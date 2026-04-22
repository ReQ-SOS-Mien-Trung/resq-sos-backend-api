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
            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                return strategy.Execute(() =>
                {
                    using var dbContextTransaction = _context.Database.BeginTransaction();
                    try
                    {
                        var result = _context.SaveChanges();
                        dbContextTransaction.Commit();
                        return result;
                    }
                    catch
                    {
                        dbContextTransaction.Rollback();
                        throw;
                    }
                });
            }
            catch
            {
                return -1;
            }
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
            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var dbContextTransaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var result = await _context.SaveChangesAsync();
                        await dbContextTransaction.CommitAsync();
                        return result;
                    }
                    catch
                    {
                        await dbContextTransaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch
            {
                return -1;
            }
        }

        public void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class
        {
            _context.Entry(entity).State = EntityState.Unchanged;
        }

        public void ClearTrackedChanges()
        {
            _context.ChangeTracker.Clear();
        }

        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            // If already inside an active transaction (nested call), just execute the action
            // and participate in the outer transaction — do NOT begin a new one.
            if (_context.Database.CurrentTransaction != null)
            {
                await action();
                return;
            }

            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
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
            });
        }
    }
}
