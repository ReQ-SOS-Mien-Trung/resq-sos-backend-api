using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services.Ai;
using RESQ.Application.UseCases.SystemConfig.Commands.AiConfigVersioning;

namespace RESQ.Application.UseCases.SystemConfig.Commands.RollbackAiConfigVersion;

public class RollbackAiConfigVersionCommandHandler(
    IAiConfigRepository aiConfigRepository,
    IUnitOfWork unitOfWork,
    ILogger<RollbackAiConfigVersionCommandHandler> logger) : IRequestHandler<RollbackAiConfigVersionCommand, AiConfigVersionActionResponse>
{
    private readonly IAiConfigRepository _aiConfigRepository = aiConfigRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<RollbackAiConfigVersionCommandHandler> _logger = logger;

    public async Task<AiConfigVersionActionResponse> Handle(RollbackAiConfigVersionCommand request, CancellationToken cancellationToken)
    {
        AiConfigVersionActionResponse? response = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var target = await _aiConfigRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy AI config với Id={request.Id}");

            if (target.IsActive)
            {
                throw new BadRequestException("AI config này đã ở trạng thái active.");
            }

            if (string.IsNullOrWhiteSpace(target.ApiKey))
            {
                throw new BadRequestException("Không thể rollback AI config khi chưa cấu hình api_key.");
            }

            var now = DateTime.UtcNow;
            var versions = await _aiConfigRepository.GetVersionsAsync(cancellationToken);
            foreach (var version in versions.Where(x => x.IsActive && x.Id != target.Id))
            {
                version.IsActive = false;
                version.Version = PromptLifecycleStatusResolver.NormalizeReleasedVersion(version.Version);
                version.ApiUrl = AiProviderDefaults.ResolveApiUrl(version.Provider);
                version.UpdatedAt = now;
                await _aiConfigRepository.UpdateAsync(version, cancellationToken);
            }

            target.IsActive = true;
            target.Version = PromptLifecycleStatusResolver.NormalizeReleasedVersion(target.Version);
            target.ApiUrl = AiProviderDefaults.ResolveApiUrl(target.Provider);
            target.UpdatedAt = now;
            await _aiConfigRepository.UpdateAsync(target, cancellationToken);

            await _unitOfWork.SaveAsync();

            response = new AiConfigVersionActionResponse
            {
                Id = target.Id,
                Name = target.Name,
                Version = target.Version,
                Status = PromptLifecycleStatusResolver.DetermineStatus(target),
                Message = "Rollback AI config version thành công."
            };
        });

        _logger.LogInformation("Rolled back to AI config version Id={Id}", request.Id);

        return response ?? throw new NotFoundException($"Không tìm thấy AI config với Id={request.Id}");
    }
}
