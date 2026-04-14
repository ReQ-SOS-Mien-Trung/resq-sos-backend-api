using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.AddDisbursementItems;

/// <summary>
/// DepotManager báo cáo v?t ph?m dã mua sau khi nh?n ti?n (Cách 1).
/// Admin cung có quy?n d? override n?u c?n.
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
