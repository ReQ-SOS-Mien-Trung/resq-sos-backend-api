using RESQ.Domain.Entities.Logistics.Exceptions;

namespace RESQ.Domain.Entities.Logistics;

public class PurchasedInventoryItemModel
{
    public int Id { get; set; }
    public int VatInvoiceId { get; set; }
    public int ReliefItemId { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public DateOnly? ReceivedDate { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public string? Notes { get; set; }
    public Guid ReceivedBy { get; set; }
    public int ReceivedAt { get; set; }
    public DateTime? CreatedAt { get; set; }

    public static PurchasedInventoryItemModel Create(
        int vatInvoiceId,
        int reliefItemId,
        int quantity,
        decimal? unitPrice,
        DateOnly? receivedDate,
        DateOnly? expiredDate,
        string? notes,
        Guid receivedBy,
        int receivedAt)
    {
        if (quantity <= 0)
            throw new InvalidReliefItemQuantityException(quantity);

        return new PurchasedInventoryItemModel
        {
            VatInvoiceId = vatInvoiceId,
            ReliefItemId = reliefItemId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            ReceivedDate = receivedDate,
            ExpiredDate = expiredDate,
            Notes = notes,
            ReceivedBy = receivedBy,
            ReceivedAt = receivedAt,
            CreatedAt = DateTime.UtcNow
        };
    }
}
