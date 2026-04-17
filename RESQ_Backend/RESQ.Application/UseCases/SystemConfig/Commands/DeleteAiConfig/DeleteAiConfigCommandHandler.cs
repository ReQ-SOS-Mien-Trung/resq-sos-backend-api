using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.DeleteAiConfig;

public class DeleteAiConfigCommandHandler(
    IAiConfigRepository aiConfigRepository,
    IUnitOfWork unitOfWork,
    ILogger<DeleteAiConfigCommandHandler> logger) : IRequestHandler<DeleteAiConfigCommand>
{
    private readonly IAiConfigRepository _aiConfigRepository = aiConfigRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<DeleteAiConfigCommandHandler> _logger = logger;

    public async Task Handle(DeleteAiConfigCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting AI config Id={Id}", request.Id);

        var aiConfig = await _aiConfigRepository.GetByIdAsync(request.Id, cancellationToken);
        if (aiConfig == null)
        {
            throw new NotFoundException($"Không tìm thấy AI config với Id={request.Id}");
        }

        if (!PromptLifecycleStatusResolver.IsDraft(aiConfig))
        {
            throw new BadRequestException("Chỉ draft AI config mới có thể xóa.");
        }

        await _aiConfigRepository.DeleteAsync(request.Id, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Deleted AI config successfully: Id={Id}", request.Id);
    }
}
