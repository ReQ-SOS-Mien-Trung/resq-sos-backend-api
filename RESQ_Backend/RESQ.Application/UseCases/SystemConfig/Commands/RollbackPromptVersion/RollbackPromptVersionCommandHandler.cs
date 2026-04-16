using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.SystemConfig.Commands.PromptVersioning;

namespace RESQ.Application.UseCases.SystemConfig.Commands.RollbackPromptVersion;

public class RollbackPromptVersionCommandHandler(
    IPromptRepository promptRepository,
    IUnitOfWork unitOfWork,
    ILogger<RollbackPromptVersionCommandHandler> logger) : IRequestHandler<RollbackPromptVersionCommand, PromptVersionActionResponse>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<RollbackPromptVersionCommandHandler> _logger = logger;

    public async Task<PromptVersionActionResponse> Handle(RollbackPromptVersionCommand request, CancellationToken cancellationToken)
    {
        PromptVersionActionResponse? response = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var target = await _promptRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException($"Khong tim thay prompt voi Id={request.Id}");

            if (target.IsActive)
            {
                throw new BadRequestException("Prompt nay da o trang thai active.");
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
                Message = "Rollback prompt version thanh cong."
            };
        });

        _logger.LogInformation("Rolled back to prompt version Id={Id}", request.Id);

        return response ?? throw new NotFoundException($"Khong tim thay prompt voi Id={request.Id}");
    }
}
