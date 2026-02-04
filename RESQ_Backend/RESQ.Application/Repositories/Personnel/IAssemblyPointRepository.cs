using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.Personnel;

namespace RESQ.Application.Repositories.Personnel;

public interface IAssemblyPointRepository
{
    Task CreateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(AssemblyPointModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    
    Task<AssemblyPointModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<AssemblyPointModel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<AssemblyPointModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default); // Added
    
    Task<PagedResult<AssemblyPointModel>> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
