using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Mappers.Emergency;

namespace RESQ.Infrastructure.Persistence.Emergency;

public class SosRuleEvaluationRepository(IUnitOfWork unitOfWork) : ISosRuleEvaluationRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task CreateAsync(SosRuleEvaluationModel evaluation, CancellationToken cancellationToken = default)
    {
        var entity = SosRuleEvaluationMapper.ToEntity(evaluation);
        await _unitOfWork.GetRepository<SosRuleEvaluation>().AddAsync(entity);
    }

    public async Task<SosRuleEvaluationModel?> GetBySosRequestIdAsync(int sosRequestId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<SosRuleEvaluation>()
            .GetByPropertyAsync(x => x.SosRequestId == sosRequestId, tracked: false);

        return entity == null ? null : SosRuleEvaluationMapper.ToDomain(entity);
    }
}
