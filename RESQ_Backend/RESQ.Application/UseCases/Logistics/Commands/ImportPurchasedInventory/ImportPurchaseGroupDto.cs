namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchaseGroupDto
{
    public VatInvoiceDto VatInvoice { get; set; } = new();
    public List<ImportPurchasedItemDto> Items { get; set; } = new();

    /// <summary>
    /// (Tuỳ chọn) Liên kết hóa đơn này với một CampaignDisbursement (Cách 1 - Admin cấp phát).
    /// Nếu được cung cấp, toàn bộ vật phẩm hợp lệ trong nhóm sẽ tự động
    /// được ghi vào bảng disbursement_items để donor xem công khai.
    /// Không cần gọi POST /finance/disbursements/{id}/items thêm nữa.
    /// </summary>
    public int? CampaignDisbursementId { get; set; }
}
