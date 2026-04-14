using RESQ.Application.Common.Models;

namespace RESQ.Application.Common.Logistics;

public class UpcomingReturnActivityListItem
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public int MissionId { get; set; }
    public string? MissionType { get; set; }
    public string? MissionStatus { get; set; }
    public DateTime? MissionStartTime { get; set; }
    public DateTime? MissionExpectedEndTime { get; set; }
    public int ActivityId { get; set; }
    public int? Step { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public int? EstimatedTime { get; set; }
    public string? Status { get; set; }
    public DateTime? AssignedAt { get; set; }
    public int? MissionTeamId { get; set; }
    public int? RescueTeamId { get; set; }
    public string? RescueTeamName { get; set; }
    public string? TeamType { get; set; }
    public List<ReturnSupplyActivityItemDetail> Items { get; set; } = [];
}

public class ReturnHistoryActivityListItem
{
    public int DepotId { get; set; }
    public string? DepotName { get; set; }
    public string? DepotAddress { get; set; }
    public int MissionId { get; set; }
    public string? MissionType { get; set; }
    public string? MissionStatus { get; set; }
    public DateTime? MissionStartTime { get; set; }
    public DateTime? MissionExpectedEndTime { get; set; }
    public int ActivityId { get; set; }
    public int? Step { get; set; }
    public string? ActivityType { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public int? EstimatedTime { get; set; }
    public string? Status { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedBy { get; set; }
    public string? CompletedByName { get; set; }
    public int? MissionTeamId { get; set; }
    public int? RescueTeamId { get; set; }
    public string? RescueTeamName { get; set; }
    public string? TeamType { get; set; }
    public List<ReturnSupplyActivityItemDetail> Items { get; set; } = [];
}

public class ReturnSupplyActivityItemDetail
{
    public int? ItemId { get; set; }
    public string? ItemName { get; set; }
    public int Quantity { get; set; }
    public string? Unit { get; set; }
    public int? ActualReturnedQuantity { get; set; }
    public List<SupplyExecutionReusableUnitDto> ExpectedReturnUnits { get; set; } = [];
    public List<SupplyExecutionReusableUnitDto> ReturnedReusableUnits { get; set; } = [];
}
