namespace RESQ.Application.Repositories.Base;

public interface IUnitOfWork
{
    IGenericRepository<T> GetRepository<T>() where T : class;
    int SaveChangesWithTransaction();
    Task<int> SaveChangesWithTransactionAsync();
    Task<int> SaveAsync();
    void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    /// Thực thi một hành động trong transaction scope.
    /// Nếu exception xảy ra, toàn bộ thay đổi sẽ rollback.
    /// Tất cả SaveAsync() bên trong action đều nằm trong transaction.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> action);
}
