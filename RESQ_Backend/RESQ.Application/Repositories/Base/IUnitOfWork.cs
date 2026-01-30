namespace RESQ.Application.Repositories.Base;

public interface IUnitOfWork
{
    IGenericRepository<T> GetRepository<T>() where T : class;
    int SaveChangesWithTransaction();
    Task<int> SaveChangesWithTransactionAsync();
    Task<int> SaveAsync();
    void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class;
}
