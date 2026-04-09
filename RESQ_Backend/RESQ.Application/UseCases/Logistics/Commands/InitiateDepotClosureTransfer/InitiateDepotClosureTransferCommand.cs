using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosureTransfer;

/// <summary>
/// Admin chuyển toàn bộ hàng tồn sang kho khác để hoàn tất đóng kho.
/// Tự động tạo bản ghi đóng kho và bản ghi chuyển kho nội bộ.
/// </summary>
public record InitiateDepotClosureTransferCommand(
    int DepotId,
    int TargetDepotId,
    Guid InitiatedBy,
    string? Reason
) : IRequest<InitiateDepotClosureTransferResponse>;
