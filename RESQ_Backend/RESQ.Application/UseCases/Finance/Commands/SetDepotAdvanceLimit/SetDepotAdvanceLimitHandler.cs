using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Finance.Commands.SetDepotAdvanceLimit;

public class SetDepotAdvanceLimitHandler : IRequestHandler<SetDepotAdvanceLimitCommand, Unit>
{
    private readonly IDepotRepository _depotRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAdminRealtimeHubService _adminRealtimeHubService;

    public SetDepotAdvanceLimitHandler(
        IDepotRepository depotRepo,
        IUnitOfWork unitOfWork,
        IAdminRealtimeHubService adminRealtimeHubService)
    {
        _depotRepo = depotRepo;
        _unitOfWork = unitOfWork;
        _adminRealtimeHubService = adminRealtimeHubService;
    }

    public async Task<Unit> Handle(SetDepotAdvanceLimitCommand request, CancellationToken cancellationToken)
    {
        if (request.AdvanceLimit < 0)
        {
            throw new BadRequestException("Hạn mức ứng trước không được là số âm.");
        }

        var depot = await _depotRepo.GetByIdAsync(request.DepotId, cancellationToken);
        if (depot == null)
        {
            throw new NotFoundException($"Không tìm thấy kho có id {request.DepotId}.");
        }

        depot.SetAdvanceLimit(request.AdvanceLimit);
        await _depotRepo.UpdateAsync(depot, cancellationToken);
        await _unitOfWork.SaveAsync();

        await _adminRealtimeHubService.PushDisbursementUpdateAsync(
            new AdminDisbursementRealtimeUpdate
            {
                EntityId = depot.Id,
                EntityType = "DepotFund",
                DepotId = depot.Id,
                Amount = request.AdvanceLimit,
                Action = "AdvanceLimitUpdated",
                Status = depot.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);

        await _adminRealtimeHubService.PushDepotUpdateAsync(
            new AdminDepotRealtimeUpdate
            {
                EntityId = depot.Id,
                EntityType = "Depot",
                DepotId = depot.Id,
                Action = "AdvanceLimitUpdated",
                Status = depot.Status.ToString(),
                ChangedAt = DateTime.UtcNow
            },
            cancellationToken);

        return Unit.Value;
    }
}
