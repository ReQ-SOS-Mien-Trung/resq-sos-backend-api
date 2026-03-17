namespace RESQ.Domain.Entities.Logistics;

public class VatInvoiceItemModel
{
    public int Id { get; set; }
    public int VatInvoiceId { get; set; }
    public int ItemModelId { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public DateTime? CreatedAt { get; set; }

    public static VatInvoiceItemModel Create(int vatInvoiceId, int itemModelId, int quantity, decimal? unitPrice)
    {
        return new VatInvoiceItemModel
        {
            VatInvoiceId = vatInvoiceId,
            ItemModelId = itemModelId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            CreatedAt = DateTime.UtcNow
        };
    }
}
