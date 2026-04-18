using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Mappers.Emergency;

namespace RESQ.Infrastructure.Persistence.Emergency;

public class SosAiAnalysisRepository(IUnitOfWork unitOfWork) : ISosAiAnalysisRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task CreateAsync(SosAiAnalysisModel analysis, CancellationToken cancellationToken = default)
    {
        var entity = SosAiAnalysisMapper.ToEntity(analysis);
        await _unitOfWork.GetRepository<SosAiAnalysis>().AddAsync(entity);
    }

    public async Task<SosAiAnalysisModel?> GetBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SosAiAnalysis>()
            .GetByPropertyAsync(x => x.SosRequestId == sosRequestId, tracked: false);

        return entity == null ? null : SosAiAnalysisMapper.ToDomain(entity);
    }

    public async Task<IEnumerable<SosAiAnalysisModel>> GetAllBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<SosAiAnalysis>()
            .GetAllByPropertyAsync(x => x.SosRequestId == sosRequestId);

        return entities.Select(SosAiAnalysisMapper.ToDomain);
    }
}
