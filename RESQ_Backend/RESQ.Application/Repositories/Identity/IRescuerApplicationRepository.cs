using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.Repositories.Identity
{
    public interface IRescuerApplicationRepository
    {
        Task<RescuerApplicationModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<RescuerApplicationModel?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<RescuerApplicationModel?> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<RescuerApplicationDto?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<PagedResult<RescuerApplicationListItemDto>> GetPagedAsync(int pageNumber, int pageSize, string? status = null, string? name = null, string? email = null, string? phone = null, string? rescuerType = null, CancellationToken cancellationToken = default);
        Task<RescuerApplicationDto?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<int> CreateAsync(RescuerApplicationModel application, CancellationToken cancellationToken = default);
        Task UpdateAsync(RescuerApplicationModel application, CancellationToken cancellationToken = default);
        Task AddDocumentsAsync(int applicationId, List<RescuerApplicationDocumentModel> documents, CancellationToken cancellationToken = default);
        Task ReplaceDocumentsAsync(int applicationId, List<RescuerApplicationDocumentModel> documents, CancellationToken cancellationToken = default);
        Task<List<RescuerApplicationDocumentModel>> GetDocumentsByApplicationIdAsync(int applicationId, CancellationToken cancellationToken = default);
    }
}
