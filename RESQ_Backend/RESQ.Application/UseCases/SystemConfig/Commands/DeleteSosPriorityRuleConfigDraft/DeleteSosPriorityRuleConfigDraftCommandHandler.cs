using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.SystemConfig.Commands.DeleteSosPriorityRuleConfigDraft;

public class DeleteSosPriorityRuleConfigDraftCommandHandler(
    ISosPriorityRuleConfigRepository repository,
    IUnitOfWork unitOfWork,
    IAdminRealtimeHubService adminRealtimeHubService) : IRequestHandler<DeleteSosPriorityRuleConfigDraftCommand>
{
    private readonly ISosPriorityRuleConfigRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;

    public async Task Handle(DeleteSosPriorityRuleConfigDraftCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Cấu hình quy tắc ưu tiên SOS với Id={request.Id} không tồn tại.");

        if (!existing.IsDraft)
        {
            throw new BadRequestException("Chỉ draft config mới có thể xóa.");
        }

        await _repository.DeleteAsync(request.Id, cancellationToken);
        await _unitOfWork.SaveAsync();

        await _adminRealtimeHubService.PushSystemConfigUpdateAsync(new AdminSystemConfigRealtimeUpdate
        {
            EntityId = request.Id,
            EntityType = "SosPriorityRuleConfig",
            ConfigKey = "sos-priority-rule",
            Action = "Deleted",
            Status = "Deleted",
            ChangedAt = DateTime.UtcNow
        }, cancellationToken);
    }
}
