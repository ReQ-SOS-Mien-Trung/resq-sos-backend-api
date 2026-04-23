using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.UseCases.SystemConfig.Commands.PromptVersioning;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.RollbackPromptVersion;

public class RollbackPromptVersionCommandHandler(
    IPromptRepository promptRepository,
    IUnitOfWork unitOfWork,
    IAdminRealtimeHubService adminRealtimeHubService,
    ILogger<RollbackPromptVersionCommandHandler> logger) : IRequestHandler<RollbackPromptVersionCommand, PromptVersionActionResponse>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
    private readonly ILogger<RollbackPromptVersionCommandHandler> _logger = logger;

    public async Task<PromptVersionActionResponse> Handle(RollbackPromptVersionCommand request, CancellationToken cancellationToken)
    {
        PromptVersionActionResponse? response = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var target = await _promptRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy prompt với Id={request.Id}");

            if (target.PromptType == PromptType.MissionPlanning)
            {
                throw new BadRequestException("Prompt type 'MissionPlanning' da bi ngung ho tro va khong the rollback.");
            }

            if (target.IsActive)
            {
                throw new BadRequestException("Prompt này đã ở trạng thái active.");
            }

            var now = DateTime.UtcNow;
            var versions = await _promptRepository.GetVersionsByTypeAsync(target.PromptType, cancellationToken);
            foreach (var version in versions.Where(x => x.IsActive && x.Id != target.Id))
            {
                version.IsActive = false;
                version.Version = PromptLifecycleStatusResolver.NormalizeReleasedVersion(version.Version);
                version.UpdatedAt = now;
                await _promptRepository.UpdateAsync(version, cancellationToken);
            }

            target.IsActive = true;
            target.Version = PromptLifecycleStatusResolver.NormalizeReleasedVersion(target.Version);
            target.UpdatedAt = now;
            await _promptRepository.UpdateAsync(target, cancellationToken);

            await _unitOfWork.SaveAsync();

            response = new PromptVersionActionResponse
            {
                Id = target.Id,
                Name = target.Name,
                PromptType = target.PromptType,
                Version = target.Version,
                Status = PromptLifecycleStatusResolver.DetermineStatus(target),
                Message = "Rollback prompt version thành công."
            };
        });

        _logger.LogInformation("Rolled back to prompt version Id={Id}", request.Id);

        if (response != null)
        {
            await _adminRealtimeHubService.PushAiConfigUpdateAsync(new AdminAiConfigRealtimeUpdate
            {
                EntityId = response.Id,
                ConfigId = response.Id,
                EntityType = "Prompt",
                ConfigScope = "Prompt",
                Action = "RolledBack",
                Status = response.Status,
                ChangedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        return response ?? throw new NotFoundException($"Không tìm thấy prompt với Id={request.Id}");
    }
}
