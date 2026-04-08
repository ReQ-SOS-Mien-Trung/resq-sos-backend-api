using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.ActivateSosPriorityRuleConfig;

public class ActivateSosPriorityRuleConfigCommandHandler(
    ISosPriorityRuleConfigRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ActivateSosPriorityRuleConfigCommand, SosPriorityRuleConfigResponse>
{
    private readonly ISosPriorityRuleConfigRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<SosPriorityRuleConfigResponse> Handle(ActivateSosPriorityRuleConfigCommand request, CancellationToken cancellationToken)
    {
        var target = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Cấu hình quy tắc ưu tiên SOS với Id={request.Id} không tồn tại.");

        if (!target.IsDraft)
        {
            throw new BadRequestException("Chỉ draft config mới có thể activate.");
        }

        if (await _repository.ExistsConfigVersionAsync(target.ConfigVersion, target.Id, cancellationToken))
        {
            throw new BadRequestException($"config_version '{target.ConfigVersion}' đã tồn tại.");
        }

        var now = DateTime.UtcNow;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var versions = await _repository.GetAllAsync(cancellationToken);
            foreach (var version in versions.Where(x => x.IsActive && x.Id != target.Id))
            {
                version.IsActive = false;
                version.UpdatedAt = now;
                var archivedDocument = SosPriorityRuleConfigSupport.FromModel(version);
                archivedDocument.IsActive = false;
                SosPriorityRuleConfigSupport.SyncLegacyFields(version, archivedDocument);
                await _repository.UpdateAsync(version, cancellationToken);
            }

            target.IsActive = true;
            target.ActivatedAt = now;
            target.ActivatedBy = request.ActivatedBy;
            target.UpdatedAt = now;
            var activeDocument = SosPriorityRuleConfigSupport.FromModel(target);
            activeDocument.IsActive = true;
            SosPriorityRuleConfigSupport.SyncLegacyFields(target, activeDocument);
            await _repository.UpdateAsync(target, cancellationToken);

            await _unitOfWork.SaveAsync();
        });

        var activated = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Không thể tải config vừa activate với Id={request.Id}.");

        return ToResponse(activated);
    }

    private static SosPriorityRuleConfigResponse ToResponse(SosPriorityRuleConfigModel config)
    {
        var document = SosPriorityRuleConfigSupport.FromModel(config);
        return new SosPriorityRuleConfigResponse
        {
            Id = config.Id,
            Status = "Active",
            CreatedAt = config.CreatedAt,
            CreatedBy = config.CreatedBy,
            ActivatedAt = config.ActivatedAt,
            ActivatedBy = config.ActivatedBy,
            UpdatedAt = config.UpdatedAt,
            ConfigVersion = document.ConfigVersion,
            IsActive = document.IsActive,
            MedicalSevereIssues = document.MedicalSevereIssues,
            RequestTypeScores = document.RequestTypeScores,
            PriorityScore = document.PriorityScore,
            MedicalScore = document.MedicalScore,
            ReliefScore = document.ReliefScore,
            SituationMultiplier = document.SituationMultiplier,
            PriorityLevel = document.PriorityLevel,
            UiConstraints = document.UiConstraints,
            UiOptions = document.UiOptions
        };
    }
}
