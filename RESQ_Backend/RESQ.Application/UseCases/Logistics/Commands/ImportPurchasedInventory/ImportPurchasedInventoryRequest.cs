namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryRequest
{
    /// <summary>
    /// ID quỹ kho được chọn để chi tiêu cho lần nhập hàng này.
    /// Lấy Id từ GET /finance/depot-funds/my.
    /// Nếu null → hệ thống dùng quỹ legacy đầu tiên có số dư > 0.
    /// </summary>
    public int? DepotFundId { get; set; }

    public string? AdvancedByName { get; set; }
    public List<ImportPurchaseGroupDto> Invoices { get; set; } = new();
}
