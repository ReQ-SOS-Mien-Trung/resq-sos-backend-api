using MediatR;
using RESQ.Application.Common;
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

        if (!existing.IsDraft)
        {
            throw new BadRequestException("Chỉ có thể chỉnh sửa draft config. Hãy clone active config để tạo draft mới.");
        }

        request.Config.IsActive = false;
        if (await _repository.ExistsConfigVersionAsync(request.Config.ConfigVersion, request.Id, cancellationToken))
        {
            throw new BadRequestException($"config_version '{request.Config.ConfigVersion}' đã tồn tại.");
        }

        SosPriorityRuleConfigSupport.SyncLegacyFields(existing, request.Config);
        existing.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(existing, cancellationToken);
        await _unitOfWork.SaveAsync();

        var responseConfig = SosPriorityRuleConfigSupport.FromModel(existing);
        return new SosPriorityRuleConfigResponse
        {
            Id = existing.Id,
            Status = "Draft",
            CreatedAt = existing.CreatedAt,
            CreatedBy = existing.CreatedBy,
            ActivatedAt = existing.ActivatedAt,
            ActivatedBy = existing.ActivatedBy,
            ConfigVersion = responseConfig.ConfigVersion,
            IsActive = responseConfig.IsActive,
            MedicalSevereIssues = responseConfig.MedicalSevereIssues,
            RequestTypeScores = responseConfig.RequestTypeScores,
            PriorityScore = responseConfig.PriorityScore,
            MedicalScore = responseConfig.MedicalScore,
            ReliefScore = responseConfig.ReliefScore,
            SituationMultiplier = responseConfig.SituationMultiplier,
            PriorityLevel = responseConfig.PriorityLevel,
            UiConstraints = responseConfig.UiConstraints,
            UiOptions = responseConfig.UiOptions,
            UpdatedAt = existing.UpdatedAt
        };
    }
}
