using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Mappers.Emergency;
using Microsoft.EntityFrameworkCore;

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
        var entity = await _unitOfWork.Set<SosAiAnalysis>()
            .Where(x => x.SosRequestId == sosRequestId)
            .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : SosAiAnalysisMapper.ToDomain(entity);
    }

    public async Task<IEnumerable<SosAiAnalysisModel>> GetAllBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<SosAiAnalysis>()
            .GetAllByPropertyAsync(x => x.SosRequestId == sosRequestId);

        return entities
            .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(x => x.Id)
            .Select(SosAiAnalysisMapper.ToDomain);
    }

    public async Task<IReadOnlyDictionary<int, SosAiAnalysisModel>> GetLatestBySosRequestIdsAsync(
        IEnumerable<int> sosRequestIds,
        CancellationToken cancellationToken = default)
    {
        var ids = sosRequestIds
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<int, SosAiAnalysisModel>();

        var entities = await _unitOfWork.Set<SosAiAnalysis>()
            .Where(x => x.SosRequestId.HasValue && ids.Contains(x.SosRequestId.Value))
            .ToListAsync(cancellationToken);

        return entities
            .Where(x => x.SosRequestId.HasValue)
            .GroupBy(x => x.SosRequestId!.Value)
            .Select(group => group
                .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(x => x.Id)
                .First())
            .ToDictionary(
                entity => entity.SosRequestId!.Value,
                entity => SosAiAnalysisMapper.ToDomain(entity));
    }
}
