namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosures;

public class DepotClosureDto
{
    public int Id { get; set; }
    public int DepotId { get; set; }
    public string DepotRole { get; set; } = "SourceDepot";

    /// <summary>Status of the closure session.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Depot status before closure: Available | Full</summary>
    public string PreviousStatus { get; set; } = string.Empty;

    /// <summary>Closure reason entered by admin during initiation.</summary>
    public string CloseReason { get; set; } = string.Empty;

    /// <summary>Inventory resolution type: TransferToDepot | ExternalResolution | null</summary>
    public string? ResolutionType { get; set; }

    /// <summary>Destination depot when ResolutionType = TransferToDepot.</summary>
    public int? TargetDepotId { get; set; }
    public string? TargetDepotName { get; set; }

    /// <summary>External resolution note when ResolutionType = ExternalResolution.</summary>
    public string? ExternalNote { get; set; }

    public Guid InitiatedBy { get; set; }
    public string? InitiatedByFullName { get; set; }

    public Guid? CancelledBy { get; set; }
    public string? CancelledByFullName { get; set; }
    public string? CancellationReason { get; set; }

    public int SnapshotConsumableUnits { get; set; }
    public int SnapshotReusableUnits { get; set; }

    public DateTime InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public TransferSummaryDto? Transfer { get; set; }
    public List<TransferSummaryDto> Transfers { get; set; } = [];
}

public class TransferSummaryDto
{
    public int TransferId { get; set; }
    public int? TargetDepotId { get; set; }
    public string? TargetDepotName { get; set; }

    /// <summary>AwaitingPreparation -> Preparing -> Shipping -> Completed -> Received | Cancelled</summary>
    public string Status { get; set; } = string.Empty;
}
