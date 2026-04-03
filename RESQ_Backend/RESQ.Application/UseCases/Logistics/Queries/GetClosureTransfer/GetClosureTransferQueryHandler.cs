using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetClosureTransfer;

public class GetClosureTransferQueryHandler(
    IDepotClosureTransferRepository transferRepository)
    : IRequestHandler<GetClosureTransferQuery, ClosureTransferResponse>
{
    public async Task<ClosureTransferResponse> Handle(
        GetClosureTransferQuery request,
        CancellationToken cancellationToken)
    {
        var transfer = await transferRepository.GetByIdAsync(request.TransferId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi chuyển kho #{request.TransferId}.");

        if (transfer.ClosureId != request.ClosureId || transfer.SourceDepotId != request.DepotId)
            throw new ConflictException("Bản ghi chuyển kho không khớp với thông tin được cung cấp.");

        return new ClosureTransferResponse
        {
            Id = transfer.Id,
            ClosureId = transfer.ClosureId,
            SourceDepotId = transfer.SourceDepotId,
            TargetDepotId = transfer.TargetDepotId,
            Status = transfer.Status,
            CreatedAt = transfer.CreatedAt,
            TransferDeadlineAt = transfer.TransferDeadlineAt,
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
