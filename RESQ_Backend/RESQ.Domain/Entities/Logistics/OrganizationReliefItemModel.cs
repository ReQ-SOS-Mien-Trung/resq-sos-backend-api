using RESQ.Domain.Entities.Logistics.Exceptions;

namespace RESQ.Domain.Entities.Logistics;

public class OrganizationReliefItemModel
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int ItemModelId { get; set; }
    public int Quantity { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiredDate { get; set; }
    public string? Notes { get; set; }
    public Guid ReceivedBy { get; set; }
    public int ReceivedAt { get; set; }
    public DateTime? CreatedAt { get; set; }

    // Domain Factory Method
    public static OrganizationReliefItemModel Create(
        int organizationId, 
        int itemModelId, 
        int quantity,
        string itemType,
        DateTime? receivedDate, 
        DateTime? expiredDate, 
        string? notes, 
        Guid receivedBy, 
        int receivedAt)
    {
        if (quantity <= 0)
            throw new InvalidReliefItemQuantityException(quantity);

        return new OrganizationReliefItemModel
        {
            OrganizationId = organizationId,
            ItemModelId = itemModelId,
            Quantity = quantity,
            ItemType = itemType,
            ReceivedDate = receivedDate,
            ExpiredDate = expiredDate,
            Notes = notes,
            ReceivedBy = receivedBy,
            ReceivedAt = receivedAt,
            CreatedAt = DateTime.UtcNow
        };
    }
}
