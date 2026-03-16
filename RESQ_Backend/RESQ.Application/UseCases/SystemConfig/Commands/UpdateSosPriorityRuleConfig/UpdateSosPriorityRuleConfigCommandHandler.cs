using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdateSosPriorityRuleConfig;

public class UpdateSosPriorityRuleConfigCommandHandler(
    ISosPriorityRuleConfigRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateSosPriorityRuleConfigCommand, SosPriorityRuleConfigResponse>
{
    private readonly ISosPriorityRuleConfigRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<SosPriorityRuleConfigResponse> Handle(UpdateSosPriorityRuleConfigCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Cấu hình quy tắc ưu tiên SOS với Id={request.Id} không tồn tại.");

        existing.IssueWeightsJson = request.IssueWeightsJson;
        existing.MedicalSevereIssuesJson = request.MedicalSevereIssuesJson;
        existing.AgeWeightsJson = request.AgeWeightsJson;
        existing.RequestTypeScoresJson = request.RequestTypeScoresJson;
        existing.SituationMultipliersJson = request.SituationMultipliersJson;
        existing.PriorityThresholdsJson = request.PriorityThresholdsJson;
        existing.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(existing, cancellationToken);
        await _unitOfWork.SaveAsync();

        return new SosPriorityRuleConfigResponse
        {
            Id = existing.Id,
            IssueWeightsJson = existing.IssueWeightsJson,
            MedicalSevereIssuesJson = existing.MedicalSevereIssuesJson,
            AgeWeightsJson = existing.AgeWeightsJson,
            RequestTypeScoresJson = existing.RequestTypeScoresJson,
            SituationMultipliersJson = existing.SituationMultipliersJson,
            PriorityThresholdsJson = existing.PriorityThresholdsJson,
            UpdatedAt = existing.UpdatedAt
        };
    }
}
