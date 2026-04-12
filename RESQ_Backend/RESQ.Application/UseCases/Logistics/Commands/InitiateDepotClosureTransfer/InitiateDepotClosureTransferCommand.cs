using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosureTransfer;

/// <summary>
/// Admin phân bổ hàng tồn sang một hoặc nhiều kho khác để hoàn tất đóng kho.
/// Tự động tạo bản ghi đóng kho và các bản ghi chuyển kho nội bộ tương ứng.
/// </summary>
public record InitiateDepotClosureTransferCommand(
    int DepotId,
    Guid InitiatedBy,
    string? Reason,
    IReadOnlyCollection<InitiateDepotClosureTransferDepotAssignmentCommandItem> Assignments
) : IRequest<InitiateDepotClosureTransferResponse>;

public record InitiateDepotClosureTransferDepotAssignmentCommandItem(
    int TargetDepotId,
    IReadOnlyCollection<InitiateDepotClosureTransferAssignmentCommandItem> Items);

public record InitiateDepotClosureTransferAssignmentCommandItem(
    int ItemModelId,
    string ItemType,
    int Quantity);
