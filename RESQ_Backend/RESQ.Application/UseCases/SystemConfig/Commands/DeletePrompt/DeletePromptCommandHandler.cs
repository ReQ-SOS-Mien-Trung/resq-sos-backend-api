using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.SystemConfig.Commands.DeletePrompt;

public class DeletePromptCommandHandler(
    IPromptRepository promptRepository,
    IUnitOfWork unitOfWork,
    IAdminRealtimeHubService adminRealtimeHubService,
    ILogger<DeletePromptCommandHandler> logger) : IRequestHandler<DeletePromptCommand>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
    private readonly ILogger<DeletePromptCommandHandler> _logger = logger;

    public async Task Handle(DeletePromptCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting prompt Id={Id}", request.Id);

        var prompt = await _promptRepository.GetByIdAsync(request.Id, cancellationToken);
        if (prompt == null)
        {
            throw new NotFoundException($"Không tìm thấy prompt với Id={request.Id}");
        }

            if (!PromptLifecycleStatusResolver.IsDraft(prompt))
        {
                throw new BadRequestException("Chỉ draft prompt mới có thể xóa.");
        }

        await _promptRepository.DeleteAsync(request.Id, cancellationToken);
        await _unitOfWork.SaveAsync();

        await _adminRealtimeHubService.PushAiConfigUpdateAsync(new AdminAiConfigRealtimeUpdate
        {
            EntityId = request.Id,
            ConfigId = request.Id,
            EntityType = "Prompt",
            ConfigScope = "Prompt",
            Action = "Deleted",
            Status = "Deleted",
            ChangedAt = DateTime.UtcNow
        }, cancellationToken);

        _logger.LogInformation("Deleted prompt successfully: Id={Id}", request.Id);
    }
}
