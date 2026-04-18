using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.Repositories.Identity;

public interface IDocumentFileTypeCategoryRepository
{
    Task<List<DocumentFileTypeCategoryModel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DocumentFileTypeCategoryModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DocumentFileTypeCategoryModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(DocumentFileTypeCategoryModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(DocumentFileTypeCategoryModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
