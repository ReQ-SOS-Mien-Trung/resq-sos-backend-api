using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.UseCases.SystemConfig.Commands.PromptVersioning;

namespace RESQ.Application.UseCases.SystemConfig.Commands.ActivatePromptVersion;

public class ActivatePromptVersionCommandHandler(
    IPromptRepository promptRepository,
    IUnitOfWork unitOfWork,
    IAdminRealtimeHubService adminRealtimeHubService,
    ILogger<ActivatePromptVersionCommandHandler> logger) : IRequestHandler<ActivatePromptVersionCommand, PromptVersionActionResponse>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
    private readonly ILogger<ActivatePromptVersionCommandHandler> _logger = logger;

    public async Task<PromptVersionActionResponse> Handle(ActivatePromptVersionCommand request, CancellationToken cancellationToken)
    {
        PromptVersionActionResponse? response = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var target = await _promptRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy prompt với Id={request.Id}");

            if (target.IsActive)
            {
                throw new BadRequestException("Prompt này đang ở trạng thái active.");
            }

            var normalizedVersion = PromptLifecycleStatusResolver.NormalizeReleasedVersion(target.Version);
            var releasedVersionExists = await _promptRepository.ExistsVersionAsync(
                target.PromptType,
                normalizedVersion,
                target.Id,
                cancellationToken);
            if (releasedVersionExists)
            {
                throw new ConflictException(
                    $"Version phát hành '{normalizedVersion}' của prompt type '{target.PromptType}' đã tồn tại. Hãy đổi version draft trước khi kích hoạt.");
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
            target.Version = normalizedVersion;
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
                Message = "Kích hoạt prompt version thành công."
            };
        });

        _logger.LogInformation("Activated prompt version Id={Id}", request.Id);

        if (response != null)
        {
            await _adminRealtimeHubService.PushAiConfigUpdateAsync(new AdminAiConfigRealtimeUpdate
            {
                EntityId = response.Id,
                ConfigId = response.Id,
                EntityType = "Prompt",
                ConfigScope = "Prompt",
                Action = "Activated",
                Status = response.Status,
                ChangedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        return response ?? throw new NotFoundException($"Không tìm thấy prompt với Id={request.Id}");
    }
}
