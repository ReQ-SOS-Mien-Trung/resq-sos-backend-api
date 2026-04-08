using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Common.Logistics;

public class SupplyRequestDetail
{
    public int Id { get; set; }
    public int RequestingDepotId { get; set; }
    public int SourceDepotId { get; set; }
    public SupplyRequestPriorityLevel PriorityLevel { get; set; } = SupplyRequestPriorityLevel.Medium;
    public string SourceStatus { get; set; } = string.Empty;
    public string RequestingStatus { get; set; } = string.Empty;
    public Guid RequestedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AutoRejectAt { get; set; }
    public List<(int ItemModelId, int Quantity)> Items { get; set; } = new();
}

public class SupplyRequestItemDetail
{
    public int ItemModelId { get; set; }
    public string? ItemModelName { get; set; }
    public string? Unit { get; set; }
    public int Quantity { get; set; }
}

public class SupplyRequestListItem
{
    public int Id { get; set; }
    public int RequestingDepotId { get; set; }
    public string? RequestingDepotName { get; set; }
    public int SourceDepotId { get; set; }
    public string? SourceDepotName { get; set; }
    public SupplyRequestPriorityLevel PriorityLevel { get; set; } = SupplyRequestPriorityLevel.Medium;
    public string SourceStatus { get; set; } = string.Empty;
    public string RequestingStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? RejectedReason { get; set; }
    public Guid RequestedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AutoRejectAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<SupplyRequestItemDetail> Items { get; set; } = new();
}

public class PendingSupplyRequestMonitorItem
{
    public int Id { get; set; }
    public int SourceDepotId { get; set; }
    public Guid RequestedBy { get; set; }
    public SupplyRequestPriorityLevel PriorityLevel { get; set; } = SupplyRequestPriorityLevel.Medium;
    public DateTime CreatedAt { get; set; }
    public DateTime? AutoRejectAt { get; set; }
    public bool HighEscalationNotified { get; set; }
    public bool UrgentEscalationNotified { get; set; }
}

public class DepotRequestItem
{
    public int Id { get; set; }
    public int RequestingDepotId { get; set; }
    public string? RequestingDepotName { get; set; }
    public int SourceDepotId { get; set; }
    public string? SourceDepotName { get; set; }
    public string PriorityLevel { get; set; } = string.Empty;
    public string SourceStatus { get; set; } = string.Empty;
    public string RequestingStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? AutoRejectAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}