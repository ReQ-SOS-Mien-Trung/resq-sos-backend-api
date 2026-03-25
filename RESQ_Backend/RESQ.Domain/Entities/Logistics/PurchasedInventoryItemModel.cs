using RESQ.Domain.Entities.Logistics.Exceptions;

namespace RESQ.Domain.Entities.Logistics;

public class PurchasedInventoryItemModel
{
    public int Id { get; set; }
    public int VatInvoiceId { get; set; }
    public int ItemModelId { get; set; }
    public int Quantity { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public string? Notes { get; set; }
    public string? BatchNote { get; set; }
    public string? ItemNote { get; set; }
    public Guid ReceivedBy { get; set; }
    public int ReceivedAt { get; set; }
    public DateTime? CreatedAt { get; set; }

    public static PurchasedInventoryItemModel Create(
        int vatInvoiceId,
        int itemModelId,
        int quantity,
        DateTime? receivedDate,
        DateTime? expiredDate,
        string? notes,
        Guid receivedBy,
        int receivedAt,
        string? batchNote = null,
        string? itemNote = null)
    {
        if (quantity <= 0)
            throw new InvalidReliefItemQuantityException(quantity);

        return new PurchasedInventoryItemModel
        {
            VatInvoiceId = vatInvoiceId,
            ItemModelId = itemModelId,
            Quantity = quantity,
            ReceivedDate = receivedDate,
            ExpiredDate = expiredDate,
            Notes = notes,
            BatchNote = batchNote,
            ItemNote = itemNote,
            ReceivedBy = receivedBy,
            ReceivedAt = receivedAt,
            CreatedAt = DateTime.UtcNow
        };
    }
}
