using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Repositories.Emergency;

public interface ISosAiAnalysisRepository
{
    Task CreateAsync(SosAiAnalysisModel analysis, CancellationToken cancellationToken = default);
    Task<SosAiAnalysisModel?> GetBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SosAiAnalysisModel>> GetAllBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default);
}
