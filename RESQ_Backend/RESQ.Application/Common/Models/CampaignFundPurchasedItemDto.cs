namespace RESQ.Application.Common.Models;

/// <summary>
/// Purchased item imported by using a campaign-backed depot fund.
/// Used by the public campaign spending endpoint.
/// </summary>
public class CampaignFundPurchasedItemDto
{
    public int DepotFundId { get; set; }
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public int? VatInvoiceId { get; set; }
    public string? InvoiceSerial { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SupplierName { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public decimal? InvoiceTotalAmount { get; set; }
    public DateTime? ImportedAt { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public string ItemType { get; set; } = string.Empty;
}
