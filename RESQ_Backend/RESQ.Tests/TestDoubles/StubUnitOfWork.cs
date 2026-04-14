using RESQ.Application.Repositories.Base;

namespace RESQ.Tests.TestDoubles;

internal sealed class StubUnitOfWork : IUnitOfWork
{
    public int SaveCalls { get; private set; }
    public int ExecuteInTransactionCalls { get; private set; }
    public int ClearTrackedChangesCalls { get; private set; }

    public IGenericRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();

    public IQueryable<T> Set<T>() where T : class => throw new NotImplementedException();

    public IQueryable<T> SetTracked<T>() where T : class => throw new NotImplementedException();

    public int SaveChangesWithTransaction() => 1;

    public Task<int> SaveChangesWithTransactionAsync() => Task.FromResult(1);

    public Task<int> SaveAsync()
    {
        SaveCalls++;
        return Task.FromResult(1);
    }

    public void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class
    {
    }

    public void ClearTrackedChanges()
    {
        ClearTrackedChangesCalls++;
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        ExecuteInTransactionCalls++;
        await action();
    }
}
