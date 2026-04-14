using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.UnassignDepotManager;

public class UnassignDepotManagerCommandHandler(
    IDepotRepository depotRepository,
    IUnitOfWork unitOfWork,
    ILogger<UnassignDepotManagerCommandHandler> logger)
    : IRequestHandler<UnassignDepotManagerCommand, UnassignDepotManagerResponse>
{
    public async Task<UnassignDepotManagerResponse> Handle(
        UnassignDepotManagerCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("UnassignDepotManager: depotId={DepotId}", request.DepotId);

        // 1. Validate depot tồn tại
        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy kho với ID = {request.DepotId}");

        // 2. Kiểm tra kho đang có manager
        if (depot.CurrentManager == null)
            throw new BadRequestException("Kho này hiện không có quản lý nào được gán.");

        // 3. Gọi domain method - unassign manager hiện tại + chuyển status về PendingAssignment
        depot.UnassignManager();

        // 4. Persist
        await depotRepository.UnassignManagerAsync(depot, request.RequestedBy, cancellationToken);
        await unitOfWork.SaveAsync();

        return new UnassignDepotManagerResponse
        {
            DepotId      = depot.Id,
            DepotName    = depot.Name,
            Status       = depot.Status.ToString(),
            UnassignedAt = DateTime.UtcNow
        };
    }
}
