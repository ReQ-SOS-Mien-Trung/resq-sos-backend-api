namespace RESQ.Application.UseCases.Logistics.Queries.GetMyClosureTransfers;

public class MyClosureTransferDto
{
    public int TransferId { get; set; }
    public int ClosureId { get; set; }
    public int SourceDepotId { get; set; }
    public string SourceDepotName { get; set; } = string.Empty;
    public int TargetDepotId { get; set; }
    public string TargetDepotName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public int RelatedDepotId { get; set; }
    public string RelatedDepotName { get; set; } = string.Empty;
    public int CounterpartyDepotId { get; set; }
    public string CounterpartyDepotName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int SnapshotConsumableUnits { get; set; }
    public int SnapshotReusableUnits { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
