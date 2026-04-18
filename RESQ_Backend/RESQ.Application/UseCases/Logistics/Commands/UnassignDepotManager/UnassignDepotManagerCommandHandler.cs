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

        var now = DateTime.UtcNow;
        List<Guid> unassignedUserIds;

        if (request.UserIds != null && request.UserIds.Count > 0)
        {
            // --- Selective unassign: chỉ gỡ những userId được chỉ định ---

            // Validate: tất cả userId phải là manager active của kho này
            var activeUserIds = depot.ManagerHistory
                .Where(x => x.IsActive())
                .Select(x => x.UserId)
                .ToHashSet();

            var notFound = request.UserIds.Where(uid => !activeUserIds.Contains(uid)).ToList();
            if (notFound.Count > 0)
                throw new BadRequestException(
                    $"Những ID sau không phải quản lý đang active của kho này: {string.Join(", ", notFound)}");

            depot.UnassignManagersByUserIds(request.UserIds);
            await depotRepository.UnassignSpecificManagersAsync(
                depot, request.UserIds, request.RequestedBy, cancellationToken);

            unassignedUserIds = request.UserIds.ToList();
        }
        else
        {
            // --- Unassign all: giữ nguyên logic cũ ---

            if (depot.CurrentManager == null)
                throw new BadRequestException("Kho này hiện không có quản lý nào được gán.");

            // Lấy danh sách userId trước khi unassign
            unassignedUserIds = depot.ManagerHistory
                .Where(x => x.IsActive())
                .Select(x => x.UserId)
                .ToList();

            depot.UnassignManager();
            await depotRepository.UnassignManagerAsync(depot, request.RequestedBy, cancellationToken);
        }

        await unitOfWork.SaveAsync();

        var remainingCount = depot.ManagerHistory.Count(x => x.IsActive());

        return new UnassignDepotManagerResponse
        {
            DepotId              = depot.Id,
            DepotName            = depot.Name,
            Status               = depot.Status.ToString(),
            UnassignedAt         = now,
            UnassignedUserIds    = unassignedUserIds,
            RemainingManagerCount = remainingCount
        };
    }
}
