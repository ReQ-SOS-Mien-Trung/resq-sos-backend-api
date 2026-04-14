using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosureDetail;

public class GetDepotClosureDetailQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotClosureTransferRepository transferRepository,
    IDepotInventoryRepository inventoryRepository,
    IDepotClosureExternalItemRepository externalItemRepository)
    : IRequestHandler<GetDepotClosureDetailQuery, DepotClosureDetailResponse>
{
    public async Task<DepotClosureDetailResponse> Handle(GetDepotClosureDetailQuery request, CancellationToken cancellationToken)
    {
        var closure = await closureRepository.GetByIdAsync(request.ClosureId, cancellationToken)
            ?? throw new NotFoundException("Kh�ng t�m th?y phi�n d�ng kho.");
        var transfers = await transferRepository.GetAllByClosureIdAsync(closure.Id, cancellationToken);
        var targetDepotIds = transfers.Select(x => x.TargetDepotId).Distinct().ToHashSet();

        if (request.RequestingUserId.HasValue)
        {
            var managerDepotId = await inventoryRepository.GetActiveDepotIdByManagerAsync(
                request.RequestingUserId.Value, cancellationToken);

            if (managerDepotId.HasValue)
            {
                if (managerDepotId != closure.DepotId && !targetDepotIds.Contains(managerDepotId.Value))
                    throw new ForbiddenException("B?n kh�ng ph?i l� manager c?a kho ngu?n ho?c kho d�ch trong phi�n d�ng kho n�y.");
            }
            else if (request.DepotId != closure.DepotId && !targetDepotIds.Contains(request.DepotId))
            {
                throw new NotFoundException("Kh�ng t�m th?y phi�n d�ng kho thu?c kho du?c y�u c?u.");
            }
        }
        else if (request.DepotId != closure.DepotId && !targetDepotIds.Contains(request.DepotId))
        {
            throw new NotFoundException("Kh�ng t�m th?y phi�n d�ng kho thu?c kho du?c y�u c?u.");
        }

        var depot = await depotRepository.GetByIdAsync(closure.DepotId, cancellationToken)
            ?? throw new NotFoundException("Kh�ng t�m th?y kho c?u tr?.");

        var summary = await closureRepository.GetClosureDetailAsync(closure.DepotId, request.ClosureId, cancellationToken)
            ?? throw new NotFoundException("Kh�ng t�m th?y d? li?u chi ti?t c?a phi�n d�ng kho.");

        var singleTarget = transfers.Select(x => x.TargetDepotId).Distinct().Take(2).ToList();
        int? singleTargetDepotId = singleTarget.Count == 1 ? singleTarget[0] : null;
        string? singleTargetDepotName = null;
        if (singleTargetDepotId.HasValue)
        {
            var targetDepot = await depotRepository.GetByIdAsync(singleTargetDepotId.Value, cancellationToken);
            singleTargetDepotName = targetDepot?.Name;
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
            CancelledAt = closure.CancelledAt
        };

        if (closure.ResolutionType == CloseResolutionType.TransferToDepot)
        {
            foreach (var transfer in transfers)
            {
                var items = await transferRepository.GetItemsByTransferIdAsync(transfer.Id, cancellationToken);
                response.TransferDetails.Add(new DepotClosureTransferDetailDto
                {
                    Id = transfer.Id,
                    ClosureId = transfer.ClosureId,
                    SourceDepotId = transfer.SourceDepotId,
                    TargetDepotId = transfer.TargetDepotId,
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


