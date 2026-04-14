namespace RESQ.Application.Repositories.Base;

public interface IUnitOfWork
{
    IGenericRepository<T> GetRepository<T>() where T : class;

    /// <summary>
    /// Shorthand cho GetRepository&lt;T&gt;().AsQueryable(tracked: false).
    /// Dùng cho read-only queries, tuong duong _dbContext.Set&lt;T&gt;().AsNoTracking().
    /// </summary>
    IQueryable<T> Set<T>() where T : class;

    /// <summary>
    /// Shorthand cho GetRepository&lt;T&gt;().AsQueryable(tracked: true).
    /// Dùng khi c?n track entity d? update/delete, tuong duong _dbContext.Set&lt;T&gt;().
    /// </summary>
    IQueryable<T> SetTracked<T>() where T : class;
    int SaveChangesWithTransaction();
    Task<int> SaveChangesWithTransactionAsync();
    Task<int> SaveAsync();
    void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class;

    void ClearTrackedChanges()
    {
    }

    /// <summary>
    /// Th?c thi m?t hành d?ng trong transaction scope.
    /// N?u exception x?y ra, toàn b? thay d?i s? rollback.
    /// T?t c? SaveAsync() bên trong action d?u n?m trong transaction.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> action);
}
