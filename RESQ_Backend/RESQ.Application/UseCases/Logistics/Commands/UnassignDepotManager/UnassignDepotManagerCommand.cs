using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.UnassignDepotManager;

/// <summary>
/// Nếu UserIds null/rỗng → gỡ TẤT CẢ manager đang active của kho.
/// Nếu UserIds có giá trị → chỉ gỡ những userId được chỉ định.
/// </summary>
public record UnassignDepotManagerCommand(
    int DepotId,
    Guid? RequestedBy = null,
    IReadOnlyList<Guid>? UserIds = null
) : IRequest<UnassignDepotManagerResponse>;

public class UnassignDepotManagerRequestDto
{
    /// <summary>Danh sách userId cần gỡ. Để trống = gỡ tất cả manager đang active.</summary>
    public List<Guid>? UserIds { get; set; }
}
