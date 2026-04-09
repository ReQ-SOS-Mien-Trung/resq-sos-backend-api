using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryCommand : IRequest<ImportPurchasedInventoryResponse>
{
    public Guid UserId { get; set; }
    public string? AdvancedByName { get; set; }

    /// <summary>
    /// ID quỹ kho được chọn để chi tiền cho lần nhập hàng này.
    /// Nếu null → hệ thống dùng quỹ mặc định (quỹ đầu tiên có số dư > 0, hoặc legacy fund).
    /// </summary>
    public int? DepotFundId { get; set; }

    public List<ImportPurchaseGroupDto> Invoices { get; set; } = new();
}
