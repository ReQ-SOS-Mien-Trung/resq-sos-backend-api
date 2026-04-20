using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;

/// <summary>
/// Admin nhấn "Đóng kho" - hệ thống kiểm tra điều kiện:
/// - Kho trống → đóng ngay (Success = true).
/// - Còn hàng → tạo hoặc dùng lại phiên đóng kho đang mở và trả danh sách tồn để chọn hướng xử lý.
/// </summary>
public record InitiateDepotClosureCommand(
    int DepotId,
    Guid InitiatedBy,
    string? Reason
) : IRequest<InitiateDepotClosureResponse>;
