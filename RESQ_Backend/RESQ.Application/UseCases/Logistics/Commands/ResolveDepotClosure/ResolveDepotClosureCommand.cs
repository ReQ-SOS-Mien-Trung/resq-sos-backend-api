using MediatR;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ResolveDepotClosure;

/// <summary>
/// Phase 2 đóng kho: Admin chọn cách xử lý hàng tồn và hoàn tất đóng kho.
/// </summary>
public record ResolveDepotClosureCommand(
    int DepotId,
    int ClosureId,
    Guid PerformedBy,
    CloseResolutionType ResolutionType,

    // Option 1: Chuyển sang kho khác
    int? TargetDepotId,

    // Option 2: Xử lý bên ngoài - chỉ cần ghi chú mô tả cách xử lý
    string? ExternalNote
) : IRequest<ResolveDepotClosureResponse>;
