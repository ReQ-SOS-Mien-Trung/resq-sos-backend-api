namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryLogs;

public class InventoryLogDto
{
    public int Id { get; set; }
    public int? DepotSupplyInventoryId { get; set; }
    public int? SupplyInventoryLotId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string FormattedQuantityChange { get; set; } = string.Empty;
    public int? QuantityChange { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public int? SourceId { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public string? PerformedByName { get; set; }

    /// <summary>Serial number nếu log thuộc đồ tái sử dụng.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Lot ID nếu log thuộc hàng tiêu thụ có lô.</summary>
    public int? LotId { get; set; }

    /// <summary>ID của ReusableItem tương ứng (nếu có).</summary>
    public int? ReusableItemId { get; set; }

    // VatInvoice
    public int? VatInvoiceId { get; set; }
    public string? InvoiceSerial { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierTaxCode { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public decimal? InvoiceTotalAmount { get; set; }
    public string? InvoiceFileUrl { get; set; }
}
