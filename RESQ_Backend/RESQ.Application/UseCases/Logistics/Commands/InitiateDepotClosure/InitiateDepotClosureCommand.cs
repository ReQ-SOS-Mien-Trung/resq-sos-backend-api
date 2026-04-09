using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;

/// <summary>
/// Admin nhấn "Đóng kho" — hệ thống kiểm tra điều kiện:
/// - Kho trống → đóng ngay (RequiresResolution = false).
/// - Còn hàng → trả về ClosureId + InventorySummary (RequiresResolution = true).
/// </summary>
public record InitiateDepotClosureCommand(
    int DepotId,
    Guid InitiatedBy,
    string? Reason
) : IRequest<InitiateDepotClosureResponse>;
