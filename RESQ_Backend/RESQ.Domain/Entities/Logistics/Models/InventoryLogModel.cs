namespace RESQ.Domain.Entities.Logistics.Models;

public class InventoryLogModel
{
    public int Id { get; set; }
    public int? DepotSupplyInventoryId { get; set; }
    public int? SupplyInventoryLotId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int? QuantityChange { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public int? SourceId { get; set; }
    public string? Note { get; set; }
    public string? BatchNote { get; set; }
    public string? ItemNote { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public string? PerformedByName { get; set; }
    public int? DepotId { get; set; }
    public string? DepotName { get; set; }
    public int? ItemModelId { get; set; }
    public string? ItemModelName { get; set; }
    /// <summary>Tồn hiện tại của item model trong kho tại thời điểm query, không phải tồn tại thời điểm phát sinh log.</summary>
    public int? RemainingQuantity { get; set; }

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
    public List<InventoryLogLotDetailModel> LotDetails { get; set; } = [];
    public List<InventoryLogReusableDetailModel> ReusableDetails { get; set; } = [];
}

public class InventoryLogLotDetailModel
{
    public int? LotId { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public int QuantityChange { get; set; }
}

public class InventoryLogReusableDetailModel
{
    public int? ReusableItemId { get; set; }
    public string? SerialNumber { get; set; }
    public int QuantityChange { get; set; }
}
