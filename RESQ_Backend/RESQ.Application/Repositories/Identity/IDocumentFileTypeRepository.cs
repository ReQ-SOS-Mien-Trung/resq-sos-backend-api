using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.Repositories.Identity;

public interface IDocumentFileTypeRepository
{
    Task<List<DocumentFileTypeModel>> GetAllAsync(bool? activeOnly = true, CancellationToken cancellationToken = default);
    Task<DocumentFileTypeModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DocumentFileTypeModel?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(DocumentFileTypeModel model, CancellationToken cancellationToken = default);
    Task UpdateAsync(DocumentFileTypeModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
