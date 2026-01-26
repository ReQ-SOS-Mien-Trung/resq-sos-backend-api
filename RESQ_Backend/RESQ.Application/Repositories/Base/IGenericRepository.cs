using System.Linq.Expressions;

namespace RESQ.Application.Repositories.Base
{
    public interface IGenericRepository<T> where T : class
    {
        public Task AddAsync(T entity);
        public Task UpdateAsync(T entity);
        public Task DeleteAsyncById(object id);
        public Task DeleteAsync(params object[] keyValues);
        public Task<List<T>> GetAllByPropertyAsync(Expression<Func<T, bool>>? filter = null, string? includeProperties = null);
        public Task<T?> GetByPropertyAsync(Expression<Func<T, bool>>? filter = null, bool tracked = true, string? includeProperties = null);
        Task AddRangeAsync(IEnumerable<T> entities);
    }
}
