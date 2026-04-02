using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetSupplyRequests;

public class SupplyRequestDto
{
    public int     Id                  { get; set; }
    public int     RequestingDepotId   { get; set; }
    public string? RequestingDepotName { get; set; }
    public int     SourceDepotId       { get; set; }
    public string? SourceDepotName     { get; set; }
    public SupplyRequestPriorityLevel PriorityLevel { get; set; } = SupplyRequestPriorityLevel.Medium;
    public string  SourceStatus        { get; set; } = string.Empty;
    public string  RequestingStatus    { get; set; } = string.Empty;
    public string? Note                { get; set; }
    public string? RejectedReason      { get; set; }
    public Guid    RequestedBy         { get; set; }
    public DateTime  CreatedAt         { get; set; }

    /// <summary>Thời điểm yêu cầu bị tự động từ chối (UTC+7). Dùng cho countdown trên FE.</summary>
    public DateTimeOffset ResponseDeadline { get; set; }

    /// <summary>Số giây còn lại đến deadline (>= 0). Tính tại thời điểm server xử lý request.</summary>
    public long RemainingSeconds { get; set; }

    public DateTime? RespondedAt       { get; set; }
    public DateTime? ShippedAt         { get; set; }
    public DateTime? CompletedAt       { get; set; }

    /// <summary>"Requester" — kho này tạo yêu cầu | "Source" — kho này nhận yêu cầu tiếp tế.</summary>
    public string Role { get; set; } = string.Empty;

    public List<SupplyRequestItemDto> Items { get; set; } = new();
}
