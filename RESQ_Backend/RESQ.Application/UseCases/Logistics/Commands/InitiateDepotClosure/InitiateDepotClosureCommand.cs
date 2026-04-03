using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;

/// <summary>
/// Phase 1 đóng kho: Admin nhấn "Đóng kho" — hệ thống kiểm tra điều kiện,
/// đặt soft-lock Closing và trả về thông tin tồn kho cần xử lý.
/// </summary>
public record InitiateDepotClosureCommand(
    int DepotId,
    Guid InitiatedBy,
    string Reason
) : IRequest<InitiateDepotClosureResponse>;
