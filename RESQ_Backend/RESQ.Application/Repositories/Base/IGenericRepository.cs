using System.Linq.Expressions;
using RESQ.Application.Common.Models;

namespace RESQ.Application.Repositories.Base
{
    public interface IGenericRepository<T> where T : class
    {
        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsyncById(object id);
        Task DeleteAsync(params object[] keyValues);
        Task<List<T>> GetAllByPropertyAsync(Expression<Func<T, bool>>? filter = null, string? includeProperties = null);
        Task<T?> GetByPropertyAsync(Expression<Func<T, bool>>? filter = null, bool tracked = true, string? includeProperties = null);
        
        // UPDATED: Added 'orderBy' parameter
        Task<PagedResult<T>> GetPagedAsync(
            int pageNumber, 
            int pageSize, 
            Expression<Func<T, bool>>? filter = null, 
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, 
            string? includeProperties = null
        );
        
        Task AddRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// Returns an IQueryable for complex queries (Join, GroupBy, Select projections)
        /// that go beyond the standard CRUD operations.
        /// </summary>
        IQueryable<T> AsQueryable(bool tracked = false);
    }
}
