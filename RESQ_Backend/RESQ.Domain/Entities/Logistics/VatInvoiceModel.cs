namespace RESQ.Domain.Entities.Logistics;

public class VatInvoiceModel
{
    public int Id { get; set; }
    public string? InvoiceSerial { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierTaxCode { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? FileUrl { get; set; }
    public DateTime? CreatedAt { get; set; }

    public static VatInvoiceModel Create(
        string? invoiceSerial,
        string? invoiceNumber,
        string? supplierName,
        string? supplierTaxCode,
        DateOnly? invoiceDate,
        decimal? totalAmount,
        string? fileUrl)
    {
        return new VatInvoiceModel
        {
            InvoiceSerial = invoiceSerial?.Trim(),
            InvoiceNumber = invoiceNumber?.Trim(),
            SupplierName = supplierName?.Trim(),
            SupplierTaxCode = supplierTaxCode?.Trim(),
            InvoiceDate = invoiceDate,
            TotalAmount = totalAmount,
            FileUrl = fileUrl,
            CreatedAt = DateTime.UtcNow
        };
    }
}
