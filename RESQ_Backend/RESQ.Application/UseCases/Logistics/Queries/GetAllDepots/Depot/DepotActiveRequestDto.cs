namespace RESQ.Application.UseCases.Logistics.Queries.GetAllDepots.Depot;

/// <summary>
/// Thông tin tóm tắt một yêu cầu tiếp tế liên quan đến kho (bao gồm cả đang xử lý lẫn đã hoàn thành).
/// </summary>
public class DepotRequestDto
{
    public int Id { get; set; }

    public int RequestingDepotId { get; set; }
    public string? RequestingDepotName { get; set; }

    public int SourceDepotId { get; set; }
    public string? SourceDepotName { get; set; }

    /// <summary>"Requester" - kho này tạo yêu cầu | "Source" - kho này nhận yêu cầu tiếp tế.</summary>
    public string Role { get; set; } = string.Empty;

    public string PriorityLevel { get; set; } = string.Empty;
    public string SourceStatus { get; set; } = string.Empty;
    public string RequestingStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? AutoRejectAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
