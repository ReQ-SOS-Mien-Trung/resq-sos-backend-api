using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.AddDisbursementItems;

/// <summary>
/// DepotManager báo cáo vật phẩm đã mua sau khi nhận tiền (Cách 1).
/// Admin cũng có quyền để override nếu cần.
/// </summary>
public record AddDisbursementItemsCommand(
    int DisbursementId,
    List<DisbursementItemDto> Items,
    Guid CallerId,
    bool CanManageAnyDisbursement
) : IRequest<Unit>;

public class DisbursementItemDto
{
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Note { get; set; }
}
