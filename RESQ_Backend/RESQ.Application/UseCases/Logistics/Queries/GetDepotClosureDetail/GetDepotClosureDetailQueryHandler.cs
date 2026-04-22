using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosureDetail;

public class GetDepotClosureDetailQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotClosureTransferRepository transferRepository,
    IDepotInventoryRepository inventoryRepository,
    IDepotClosureExternalItemRepository externalItemRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<GetDepotClosureDetailQuery, DepotClosureDetailResponse>
{
    public async Task<DepotClosureDetailResponse> Handle(GetDepotClosureDetailQuery request, CancellationToken cancellationToken)
    {
        var closure = await closureRepository.GetByIdAsync(request.ClosureId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phiên đóng kho.");
        var transfers = (await transferRepository.GetAllByClosureIdAsync(closure.Id, cancellationToken))
            .OrderBy(x => x.Id)
            .ToList();
        var targetDepotIds = transfers.Select(x => x.TargetDepotId).Distinct().ToHashSet();

        if (request.RequestingUserId.HasValue)
        {
            var managerDepotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(
                request.RequestingUserId.Value, cancellationToken);

            if (managerDepotId.HasValue)
            {
                if (managerDepotId != closure.DepotId && !targetDepotIds.Contains(managerDepotId.Value))
                {
                    throw new ForbiddenException("Bạn không phải là manager của kho nguồn hoặc kho đích trong phiên đóng kho này.");
                }
            }
            else if (request.DepotId != closure.DepotId && !targetDepotIds.Contains(request.DepotId))
            {
                throw new NotFoundException("Không tìm thấy phiên đóng kho thuộc kho được yêu cầu.");
            }
        }
        else if (request.DepotId != closure.DepotId && !targetDepotIds.Contains(request.DepotId))
        {
            throw new NotFoundException("Không tìm thấy phiên đóng kho thuộc kho được yêu cầu.");
        }

        var depot = await depotRepository.GetByIdAsync(closure.DepotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        var summary = await closureRepository.GetClosureDetailAsync(closure.DepotId, request.ClosureId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy dữ liệu chi tiết của phiên đóng kho.");

        var singleTarget = transfers.Select(x => x.TargetDepotId).Distinct().Take(2).ToList();
        int? singleTargetDepotId = singleTarget.Count == 1 ? singleTarget[0] : null;
        string? singleTargetDepotName = null;
        if (singleTargetDepotId.HasValue)
        {
            var targetDepot = await depotRepository.GetByIdAsync(singleTargetDepotId.Value, cancellationToken);
            singleTargetDepotName = targetDepot?.Name;
        }

        var targetDepotNames = new Dictionary<int, string>();
        foreach (var targetDepotId in transfers.Select(x => x.TargetDepotId).Distinct())
        {
            var targetDepot = await depotRepository.GetByIdAsync(targetDepotId, cancellationToken);
            if (targetDepot != null)
            {
                targetDepotNames[targetDepotId] = targetDepot.Name;
            }
        }

        var remainingInventoryItems = await depotRepository.GetDetailedInventoryForClosureAsync(closure.DepotId, cancellationToken);
        var hasOpenTransfers = transfers.Any(x =>
            !string.Equals(x.Status, "Received", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(x.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
        var hasRemainingItems = remainingInventoryItems.Count > 0;
        var hasTransferableRemainingItems = remainingInventoryItems.Any(x => x.TransferableQuantity > 0);
        var transferableRemainingItemCount = remainingInventoryItems.Count(x => x.TransferableQuantity > 0);
        var transferableRemainingUnitCount = remainingInventoryItems.Sum(x => x.TransferableQuantity);
        var blockedRemainingItemCount = remainingInventoryItems.Count(x => x.BlockedQuantity > 0);
        var blockedRemainingUnitCount = remainingInventoryItems.Sum(x => x.BlockedQuantity);

        if (!hasOpenTransfers)
        {
            var needsResidualReopen = hasRemainingItems && (
                closure.Status == DepotClosureStatus.TransferPending
                || closure.ResolutionType == CloseResolutionType.TransferToDepot);

            var needsCompletionRecovery = !hasRemainingItems
                && closure.Status == DepotClosureStatus.TransferPending;

            if (needsResidualReopen)
            {
                closure.ReopenForResidualHandling();
                await closureRepository.UpdateAsync(closure, cancellationToken);
                await unitOfWork.SaveAsync();
            }
            else if (needsCompletionRecovery)
            {
                closure.Complete(closure.CompletedAt ?? DateTime.UtcNow);
                await closureRepository.UpdateAsync(closure, cancellationToken);
                await unitOfWork.SaveAsync();
            }
        }

        var response = new DepotClosureDetailResponse
        {
            Id = closure.Id,
            DepotId = depot.Id,
            DepotName = depot.Name,
            Status = closure.Status.ToString(),
            PreviousStatus = closure.PreviousStatus.ToString(),
            CloseReason = closure.CloseReason,
            ResolutionType = closure.ResolutionType?.ToString(),
            TargetDepotId = singleTargetDepotId,
            TargetDepotName = singleTargetDepotName,
            ExternalNote = closure.ExternalNote,
            InitiatedBy = closure.InitiatedBy,
            InitiatedByFullName = summary.InitiatedByFullName,
            CancelledBy = closure.CancelledBy,
            CancelledByFullName = summary.CancelledByFullName,
            CancellationReason = closure.CancellationReason,
            SnapshotConsumableUnits = closure.SnapshotConsumableUnits,
            SnapshotReusableUnits = closure.SnapshotReusableUnits,
            ActualConsumableUnits = closure.ActualConsumableUnits,
            ActualReusableUnits = closure.ActualReusableUnits,
            DriftNote = closure.DriftNote,
            FailureReason = closure.FailureReason,
            IsForced = closure.IsForced,
            ForceReason = closure.ForceReason,
            InitiatedAt = closure.InitiatedAt,
            CompletedAt = closure.CompletedAt,
            CancelledAt = closure.CancelledAt,
            HasOpenTransfers = hasOpenTransfers,
            HasRemainingItems = hasRemainingItems,
            RemainingItemCount = remainingInventoryItems.Count,
            HasTransferableRemainingItems = hasTransferableRemainingItems,
            TransferableRemainingItemCount = transferableRemainingItemCount,
            TransferableRemainingUnitCount = transferableRemainingUnitCount,
            BlockedRemainingItemCount = blockedRemainingItemCount,
            BlockedRemainingUnitCount = blockedRemainingUnitCount,
            CanSelectResolutionOption = closure.Status == DepotClosureStatus.InProgress
                                        && closure.ResolutionType == null
                                        && hasTransferableRemainingItems
                                        && !hasOpenTransfers,
            CanConfirmClose = closure.Status == DepotClosureStatus.Completed
                              && depot.Status == DepotStatus.Closing
                              && !hasRemainingItems
                              && !hasOpenTransfers,
            RemainingInventoryItems = remainingInventoryItems
        };

        if (transfers.Count > 0)
        {
            foreach (var transfer in transfers)
            {
                var items = await transferRepository.GetItemsByTransferIdAsync(transfer.Id, cancellationToken);
                response.TransferDetails.Add(new DepotClosureTransferDetailDto
                {
                    Id = transfer.Id,
                    ClosureId = transfer.ClosureId,
                    SourceDepotId = transfer.SourceDepotId,
                    SourceDepotName = depot.Name,
                    TargetDepotId = transfer.TargetDepotId,
                    TargetDepotName = targetDepotNames.GetValueOrDefault(transfer.TargetDepotId),
                    Status = transfer.Status,
                    CreatedAt = transfer.CreatedAt,
                    SnapshotConsumableUnits = transfer.SnapshotConsumableUnits,
                    SnapshotReusableUnits = transfer.SnapshotReusableUnits,
                    ShippedAt = transfer.ShippedAt,
                    ShippedBy = transfer.ShippedBy,
                    ShipNote = transfer.ShipNote,
                    ReceivedAt = transfer.ReceivedAt,
                    ReceivedBy = transfer.ReceivedBy,
                    ReceiveNote = transfer.ReceiveNote,
                    CancelledAt = transfer.CancelledAt,
                    CancelledBy = transfer.CancelledBy,
                    CancellationReason = transfer.CancellationReason,
                    Items = items.Select(item => new DepotClosureTransferItemDetailDto
                    {
                        ItemModelId = item.ItemModelId,
                        ItemName = item.ItemName,
                        ItemType = item.ItemType,
                        Unit = item.Unit,
                        Quantity = item.Quantity
                    }).ToList()
                });
            }

            response.TransferDetail = response.TransferDetails.Count == 1
                ? response.TransferDetails[0]
                : null;
        }

        if (closure.ResolutionType == CloseResolutionType.ExternalResolution)
        {
            response.ExternalItems = (await externalItemRepository.GetByClosureIdAsync(closure.Id, cancellationToken))
                .Select(item => new DepotClosureExternalItemDetailResponse
                {
                    Id = item.Id,
                    ItemName = item.ItemName,
                    CategoryName = item.CategoryName,
                    ItemType = item.ItemType,
                    Unit = item.Unit,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice,
                    HandlingMethod = item.HandlingMethod,
                    HandlingMethodDisplay = item.HandlingMethodDisplay,
                    Recipient = item.Recipient,
                    Note = item.Note,
                    ImageUrl = item.ImageUrl,
                    ProcessedBy = item.ProcessedBy,
                    ProcessedAt = item.ProcessedAt,
                    CreatedAt = item.CreatedAt
                })
                .ToList();
        }

        return response;
    }
}
