namespace RESQ.Application.Services;

public interface IRescueMissionSuggestionService
{
    Task<RescueMissionSuggestionResult> GenerateSuggestionAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots = null,
        bool isMultiDepotRecommended = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the AI generation process as SSE events:
    /// "status" → progress messages, "chunk" → raw AI text tokens, "result" → final parsed result.
    /// </summary>
    IAsyncEnumerable<SseMissionEvent> GenerateSuggestionStreamAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots = null,
        bool isMultiDepotRecommended = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single Server-Sent Event emitted during streaming mission suggestion generation.
/// </summary>
public class SseMissionEvent
{
    /// <summary>"status" | "chunk" | "result" | "error"</summary>
    public string EventType { get; set; } = "chunk";

    /// <summary>Plain-text message for "status"/"error", raw AI token for "chunk".</summary>
    public string? Data { get; set; }

    /// <summary>Populated only for the final "result" event.</summary>
    public RescueMissionSuggestionResult? Result { get; set; }
}

/// <summary>Thông tin tóm tắt kho tiếp tế gần nhất, dùng để cung cấp context cho AI.</summary>
public class DepotSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    /// <summary>Khoảng cách (km) từ kho đến SOS request quan trọng nhất trong cluster.</summary>
    public double DistanceKm { get; set; }
    public int Capacity { get; set; }
    public int CurrentUtilization { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>Danh sách vật tư còn khả dụng (quantity - reserved > 0) trong kho này.</summary>
    public List<DepotInventoryItemDto> Inventories { get; set; } = [];
}

/// <summary>Một dòng vật tư khả dụng trong kho tiếp tế.</summary>
public class DepotInventoryItemDto
{
    /// <summary>ID của relief item trong DB (dùng để AI trả về item_id trong supplies_to_collect).</summary>
    public int? ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int AvailableQuantity { get; set; }
}

public class SosRequestSummary
{
    public int Id { get; set; }
    public string? SosType { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public string? StructuredData { get; set; }
    public string? PriorityLevel { get; set; }
    public string? Status { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? WaitTimeMinutes { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class RescueMissionSuggestionResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ModelName { get; set; }
    public double ResponseTimeMs { get; set; }

    public string? SuggestedMissionTitle { get; set; }
    public string? SuggestedMissionType { get; set; }
    public double? SuggestedPriorityScore { get; set; }
    public string? SuggestedSeverityLevel { get; set; }
    public string? OverallAssessment { get; set; }

    public List<SuggestedActivityDto> SuggestedActivities { get; set; } = [];
    public List<SuggestedResourceDto> SuggestedResources { get; set; } = [];
    public string? EstimatedDuration { get; set; }
    public string? SpecialNotes { get; set; }
    public double ConfidenceScore { get; set; }
    public string? RawAiResponse { get; set; }

    /// <summary>true khi AI có độ tự tin thấp — cần người điều phối xem xét thủ công.</summary>
    public bool NeedsManualReview { get; set; }
    /// <summary>Thông báo giải thích lý do cần xem xét thủ công (chỉ set khi NeedsManualReview = true).</summary>
    public string? LowConfidenceWarning { get; set; }
    /// <summary>true khi không có kho nào đủ đa dạng hàng để cấp phát trong một lần — AI sẽ được nhắc lấy từ nhiều kho.</summary>
    public bool MultiDepotRecommended { get; set; }

    /// <summary>Đội cứu hộ được AI đề xuất cho sứ mệnh này (populated khi agent tìm được đội phù hợp).</summary>
    public SuggestedTeamDto? SuggestedTeam { get; set; }
}

public class SupplyToCollectDto
{
    /// <summary>ID của relief item tương ứng trong kho (khớp với DepotInventoryItemDto.ItemId).</summary>
    public int? ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Unit { get; set; }
}

public class SuggestedActivityDto
{
    public int Step { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Priority { get; set; }
    public string? EstimatedTime { get; set; }
    /// <summary>ID của SOS request mà activity này phục vụ trực tiếp.</summary>
    public int? SosRequestId { get; set; }
    /// <summary>ID kho tiếp tế (chỉ có khi ActivityType = COLLECT_SUPPLIES hoặc DELIVER_SUPPLIES)</summary>
    public int? DepotId { get; set; }
    /// <summary>Tên kho tiếp tế</summary>
    public string? DepotName { get; set; }
    /// <summary>Địa chỉ kho tiếp tế</summary>
    public string? DepotAddress { get; set; }
    /// <summary>Danh sách vật tư cần lấy/giao</summary>
    public List<SupplyToCollectDto>? SuppliesToCollect { get; set; }
    /// <summary>Đội cứu hộ được AI giao thực hiện activity này.</summary>
    public SuggestedTeamDto? SuggestedTeam { get; set; }
}

public class SuggestedResourceDto
{
    public string ResourceType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? Quantity { get; set; }
    public string? Priority { get; set; }
}

/// <summary>Đội cứu hộ được AI đề xuất thực hiện sứ mệnh.</summary>
public class SuggestedTeamDto
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamType { get; set; }
    public string? Reason { get; set; }
    /// <summary>Tên điểm tập kết của đội (nếu có).</summary>
    public string? AssemblyPointName { get; set; }
    /// <summary>Vĩ độ điểm tập kết / vị trí đội.</summary>
    public double? Latitude { get; set; }
    /// <summary>Kinh độ điểm tập kết / vị trí đội.</summary>
    public double? Longitude { get; set; }
}

/// <summary>Thông tin vật tư trong kho trả về bởi searchInventory tool.</summary>
public class AgentInventoryItem
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public string? Unit { get; set; }
    public int AvailableQuantity { get; set; }
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public string? DepotAddress { get; set; }
    /// <summary>Vĩ độ kho chứa.</summary>
    public double? DepotLatitude { get; set; }
    /// <summary>Kinh độ kho chứa.</summary>
    public double? DepotLongitude { get; set; }
}

/// <summary>Thông tin đội cứu hộ trả về bởi getTeams tool.</summary>
public class AgentTeamInfo
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamType { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public int MemberCount { get; set; }
    /// <summary>Tên điểm tập kết của đội.</summary>
    public string? AssemblyPointName { get; set; }
    /// <summary>Vĩ độ điểm tập kết.</summary>
    public double? Latitude { get; set; }
    /// <summary>Kinh độ điểm tập kết.</summary>
    public double? Longitude { get; set; }
}
