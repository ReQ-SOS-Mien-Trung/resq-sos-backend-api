using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosureDetail;

public class GetDepotClosureDetailQueryHandler(
    IDepotRepository depotRepository,
    IDepotClosureRepository closureRepository,
    IDepotClosureTransferRepository transferRepository,
    IDepotClosureExternalItemRepository externalItemRepository)
    : IRequestHandler<GetDepotClosureDetailQuery, DepotClosureDetailResponse>
{
    public async Task<DepotClosureDetailResponse> Handle(GetDepotClosureDetailQuery request, CancellationToken cancellationToken)
    {
        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        var closure = await closureRepository.GetByIdAsync(request.ClosureId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phiên đóng kho.");

        if (closure.DepotId != request.DepotId)
            throw new NotFoundException("Không tìm thấy phiên đóng kho thuộc kho được yêu cầu.");

        var summary = await closureRepository.GetClosureDetailAsync(request.DepotId, request.ClosureId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy dữ liệu chi tiết của phiên đóng kho.");

        var response = new DepotClosureDetailResponse
        {
            Id = closure.Id,
            DepotId = depot.Id,
            DepotName = depot.Name,
            Status = closure.Status.ToString(),
            PreviousStatus = closure.PreviousStatus.ToString(),
            CloseReason = closure.CloseReason,
            ResolutionType = closure.ResolutionType?.ToString(),
            TargetDepotId = summary.TargetDepotId,
            TargetDepotName = summary.TargetDepotName,
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
            ClosingTimeoutAt = closure.Status is DepotClosureStatus.InProgress or DepotClosureStatus.Processing
                ? closure.ClosingTimeoutAt
                : null,
            CompletedAt = closure.CompletedAt,
            CancelledAt = closure.CancelledAt
        };

        if (closure.ResolutionType == CloseResolutionType.TransferToDepot)
        {
            var transfer = await transferRepository.GetByClosureIdAsync(closure.Id, cancellationToken);
            if (transfer != null)
            {
                response.TransferDetail = new DepotClosureTransferDetailDto
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
                    CancellationReason = transfer.CancellationReason
                };
            }
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
