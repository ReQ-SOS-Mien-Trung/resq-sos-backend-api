using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.Repositories.Logistics;

public interface IPurchasedInventoryRepository
{
    /// <summary>
    /// Kiểm tra xem hóa đơn VAT với ký hiệu và số đã cho có tồn tại trong hệ thống chưa.
    /// Chỉ kiểm tra khi cả hai đều có giá trị.
    /// </summary>
    Task<bool> ExistsBySerialAndNumberAsync(string invoiceSerial, string invoiceNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo một hóa đơn VAT mới và trả về domain model đã được lưu (có Id).
    /// </summary>
    Task<VatInvoiceModel> CreateVatInvoiceAsync(VatInvoiceModel model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm hoặc tạo các ReliefItem theo danh sách bulk.
    /// </summary>
    Task<List<ReliefItemModel>> GetOrCreateReliefItemsBulkAsync(List<ReliefItemModel> models, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk insert danh sách PurchasedInventoryItem, cập nhật DepotSupplyInventory, tạo InventoryLog với SourceType = Purchase
    /// và lưu dòng giá tương ứng vào bảng vat_invoice_items.
    /// </summary>
    Task AddPurchasedInventoryItemsBulkAsync(List<(PurchasedInventoryItemModel model, decimal? unitPrice)> items, CancellationToken cancellationToken = default);
}
