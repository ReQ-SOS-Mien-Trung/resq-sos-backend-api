using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
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
                ?? throw new NotFoundException($"Khong tim thay AI config voi Id={request.Id}");

            if (target.IsActive)
            {
                throw new BadRequestException("AI config nay dang o trang thai active.");
            }

            if (string.IsNullOrWhiteSpace(target.ApiKey))
            {
                throw new BadRequestException("Khong the kich hoat AI config khi chua cau hinh api_key.");
            }

            var normalizedVersion = PromptLifecycleStatusResolver.NormalizeReleasedVersion(target.Version);
            var releasedVersionExists = await _aiConfigRepository.ExistsVersionAsync(
                normalizedVersion,
                target.Id,
                cancellationToken);
            if (releasedVersionExists)
            {
                throw new ConflictException(
                    $"AI config version '{normalizedVersion}' da ton tai. Hay doi version draft truoc khi kich hoat.");
            }

            var now = DateTime.UtcNow;
            var versions = await _aiConfigRepository.GetVersionsAsync(cancellationToken);
            foreach (var version in versions.Where(x => x.IsActive && x.Id != target.Id))
            {
                version.IsActive = false;
                version.Version = PromptLifecycleStatusResolver.NormalizeReleasedVersion(version.Version);
                version.UpdatedAt = now;
                await _aiConfigRepository.UpdateAsync(version, cancellationToken);
            }

            target.IsActive = true;
            target.Version = normalizedVersion;
            target.UpdatedAt = now;
            await _aiConfigRepository.UpdateAsync(target, cancellationToken);

            await _unitOfWork.SaveAsync();

            response = new AiConfigVersionActionResponse
            {
                Id = target.Id,
                Name = target.Name,
                Version = target.Version,
                Status = PromptLifecycleStatusResolver.DetermineStatus(target),
                Message = "Kich hoat AI config version thanh cong."
            };
        });

        _logger.LogInformation("Activated AI config version Id={Id}", request.Id);

        return response ?? throw new NotFoundException($"Khong tim thay AI config voi Id={request.Id}");
    }
}
