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
    /// Tìm hoặc tạo các ItemModel theo danh sách bulk.
    /// </summary>
    Task<List<ItemModelRecord>> GetOrCreateReliefItemsBulkAsync(List<ItemModelRecord> models, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk insert danh sách PurchasedInventoryItem.
    /// - Consumable: cập nhật DepotSupplyInventory (quantity cộng dồn).
    /// - Reusable: tạo N bản ghi ReusableItem với serial number do hệ thống sinh tự động.
    /// Tạo InventoryLog (SourceType = Purchase), VatInvoiceItem, và cộng dồn Category.Quantity.
    /// </summary>
    Task AddPurchasedInventoryItemsBulkAsync(List<(PurchasedInventoryItemModel model, decimal? unitPrice, string itemType)> items, CancellationToken cancellationToken = default);
}
