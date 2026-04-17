using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services.Ai;
using RESQ.Application.UseCases.SystemConfig.Commands.AiConfigVersioning;

namespace RESQ.Application.UseCases.SystemConfig.Commands.ActivateAiConfigVersion;

public class ActivateAiConfigVersionCommandHandler(
    IAiConfigRepository aiConfigRepository,
    IUnitOfWork unitOfWork,
    ILogger<ActivateAiConfigVersionCommandHandler> logger) : IRequestHandler<ActivateAiConfigVersionCommand, AiConfigVersionActionResponse>
{
    private readonly IAiConfigRepository _aiConfigRepository = aiConfigRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ActivateAiConfigVersionCommandHandler> _logger = logger;

    public async Task<AiConfigVersionActionResponse> Handle(ActivateAiConfigVersionCommand request, CancellationToken cancellationToken)
    {
        AiConfigVersionActionResponse? response = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var target = await _aiConfigRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy AI config với Id={request.Id}");

            if (target.IsActive)
            {
                throw new BadRequestException("AI config này đang ở trạng thái active.");
            }

            if (string.IsNullOrWhiteSpace(target.ApiKey))
            {
                throw new BadRequestException("Không thể kích hoạt AI config khi chưa cấu hình api_key.");
            }

            var normalizedVersion = PromptLifecycleStatusResolver.NormalizeReleasedVersion(target.Version);
            var releasedVersionExists = await _aiConfigRepository.ExistsVersionAsync(
                normalizedVersion,
                target.Id,
                cancellationToken);
            if (releasedVersionExists)
            {
                throw new ConflictException(
                    $"AI config version '{normalizedVersion}' đã tồn tại. Hãy đổi version draft trước khi kích hoạt.");
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
            target.Version = normalizedVersion;
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
                Message = "Kích hoạt AI config version thành công."
            };
        });

        _logger.LogInformation("Activated AI config version Id={Id}", request.Id);

        return response ?? throw new NotFoundException($"Không tìm thấy AI config với Id={request.Id}");
    }
}
