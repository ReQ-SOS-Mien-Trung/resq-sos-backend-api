namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryTransactionHistory;

public class InventoryTransactionDto
{
    public string TransactionId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public int? SourceId { get; set; }
    public string? SourceName { get; set; }
    public string? PerformedByName { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<InventoryTransactionItemDto> Items { get; set; } = new();
    public int? VatInvoiceId { get; set; }
    public string? InvoiceSerial { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierTaxCode { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public decimal? InvoiceTotalAmount { get; set; }
    public string? InvoiceFileUrl { get; set; }
}

public class InventoryTransactionItemDto
{
    public int ItemId { get; set; }
    public int ItemModelId { get; set; }
    public int? SupplyInventoryLotId { get; set; }
    public int? LotId { get; set; }
    public int? ReusableItemId { get; set; }
    public string? SerialNumber { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int QuantityChange { get; set; }
    public string FormattedQuantityChange { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string TargetGroup { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiredDate { get; set; }
}
