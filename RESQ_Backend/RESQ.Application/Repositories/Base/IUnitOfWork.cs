namespace RESQ.Application.Repositories.Base;

public interface IUnitOfWork
{
    IGenericRepository<T> GetRepository<T>() where T : class;

    /// <summary>
    /// Shorthand cho GetRepository&lt;T&gt;().AsQueryable(tracked: false).
    /// Dùng cho read-only queries, tương đương _dbContext.Set&lt;T&gt;().AsNoTracking().
    /// </summary>
    IQueryable<T> Set<T>() where T : class;

    /// <summary>
    /// Shorthand cho GetRepository&lt;T&gt;().AsQueryable(tracked: true).
    /// Dùng khi cần track entity để update/delete, tương đương _dbContext.Set&lt;T&gt;().
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
    /// Tìm entity đang được EF Core tracking theo predicate.
    /// Dùng khi cần update in-place để tránh lỗi IdentityConflict
    /// khi cùng lúc đã load tracked entity với cùng PK.
    /// </summary>
    T? GetTracked<T>(Func<T, bool> predicate) where T : class => null;

    /// <summary>
    /// Thực thi một hành động trong transaction scope.
    /// Nếu exception xảy ra, toàn bộ thay đổi sẽ rollback.
    /// Tất cả SaveAsync() bên trong action đều nằm trong transaction.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> action);
}
