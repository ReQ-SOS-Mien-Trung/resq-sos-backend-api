namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class VatInvoiceDto
{
    public string? InvoiceSerial { get; set; }
    public string? InvoiceNumber { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierTaxCode { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? FileUrl { get; set; }
}
