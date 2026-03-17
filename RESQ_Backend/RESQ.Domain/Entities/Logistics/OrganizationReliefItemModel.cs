using RESQ.Domain.Entities.Logistics.Exceptions;

namespace RESQ.Domain.Entities.Logistics;

public class OrganizationReliefItemModel
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int ItemModelId { get; set; }
    public int Quantity { get; set; }
    public DateOnly? ReceivedDate { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public string? Notes { get; set; }
    public Guid ReceivedBy { get; set; }
    public int ReceivedAt { get; set; }
    public DateTime? CreatedAt { get; set; }

    // Domain Factory Method
    public static OrganizationReliefItemModel Create(
        int organizationId, 
        int itemModelId, 
        int quantity, 
        DateOnly? receivedDate, 
        DateOnly? expiredDate, 
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
            ReceivedDate = receivedDate,
            ExpiredDate = expiredDate,
            Notes = notes,
            ReceivedBy = receivedBy,
            ReceivedAt = receivedAt,
            CreatedAt = DateTime.UtcNow
        };
    }
}
