using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Infrastructure.Persistence.Context;
using System.Linq.Expressions;

namespace RESQ.Infrastructure.Persistence.Base;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    private readonly ResQDbContext _context;
    private readonly DbSet<T> _dbSet;

    public GenericRepository(ResQDbContext context)
    {
        _context = context;
        _dbSet = _context.Set<T>();
    }

    public async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsyncById(object id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
        }
    }
    
    public async Task DeleteAsync(params object[] keyValues)
    {
        var entity = await _dbSet.FindAsync(keyValues);
        if (entity != null)
        {
            _dbSet.Remove(entity);
        }
    }
    
    public async Task<List<T>> GetAllByPropertyAsync(Expression<Func<T, bool>>? filter = null, string? includeProperties = null)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();

        if (filter != null)
        {
            query = query.Where(filter);
        }
        
        if (includeProperties != null)
        {
            foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(includeProp.Trim());
            }
        }

        return await query.ToListAsync();
    }

    public async Task<T?> GetByPropertyAsync(Expression<Func<T, bool>>? filter = null, bool tracked = true, string? includeProperties = null)
    {
        IQueryable<T> query = _dbSet;
        if (!tracked)
        {
            query = query.AsNoTracking();
        }
        
        if (includeProperties != null)
        {
            foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(includeProp.Trim());
            }
        }

        if (filter != null)
        {
            query = query.Where(filter);
        }
        
        return await query.FirstOrDefaultAsync();
    }

    // UPDATED: Robust Implementation ensuring sequential execution
    public async Task<PagedResult<T>> GetPagedAsync(
        int pageNumber, 
        int pageSize, 
        Expression<Func<T, bool>>? filter = null, 
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, 
        string? includeProperties = null)
    {
        // 1. Create Base Query for Filters only
        IQueryable<T> baseQuery = _dbSet.AsNoTracking();

        if (filter != null)
        {
            baseQuery = baseQuery.Where(filter);
        }

        // 2. Execute Count immediately on base query (No Includes/OrderBy needed for count)
        // This effectively closes the reader for this operation before moving to the next.
        var totalCount = await baseQuery.CountAsync();

        // 3. Prepare Fetch Query from the same base
        // We reuse the filtered baseQuery but append fetch-specific operators
        IQueryable<T> fetchQuery = baseQuery;

        if (!string.IsNullOrWhiteSpace(includeProperties))
        {
            foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                fetchQuery = fetchQuery.Include(includeProp.Trim());
            }
        }

        // Apply Ordering
        if (orderBy != null)
        {
            fetchQuery = orderBy(fetchQuery);
        }

        // Apply Pagination
        var items = await fetchQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<T>(items, totalCount, pageNumber, pageSize);
    }

    public async Task AddRangeAsync(IEnumerable<T> entities)
    {
        await _dbSet.AddRangeAsync(entities);
    }
}
