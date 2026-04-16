using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
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
                ?? throw new NotFoundException($"Khong tim thay AI config voi Id={request.Id}");

            if (target.IsActive)
            {
                throw new BadRequestException("AI config nay da o trang thai active.");
            }

            if (string.IsNullOrWhiteSpace(target.ApiKey))
            {
                throw new BadRequestException("Khong the rollback AI config khi chua cau hinh api_key.");
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
            target.Version = PromptLifecycleStatusResolver.NormalizeReleasedVersion(target.Version);
            target.UpdatedAt = now;
            await _aiConfigRepository.UpdateAsync(target, cancellationToken);

            await _unitOfWork.SaveAsync();

            response = new AiConfigVersionActionResponse
            {
                Id = target.Id,
                Name = target.Name,
                Version = target.Version,
                Status = PromptLifecycleStatusResolver.DetermineStatus(target),
                Message = "Rollback AI config version thanh cong."
            };
        });

        _logger.LogInformation("Rolled back to AI config version Id={Id}", request.Id);

        return response ?? throw new NotFoundException($"Khong tim thay AI config voi Id={request.Id}");
    }
}
