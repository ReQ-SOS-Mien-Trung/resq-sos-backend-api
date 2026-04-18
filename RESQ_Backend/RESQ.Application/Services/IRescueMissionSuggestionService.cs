using RESQ.Application.Common.Models;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.Services;

public interface IRescueMissionSuggestionService
{
    Task<RescueMissionSuggestionResult> GenerateSuggestionAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots = null,
        List<AgentTeamInfo>? nearbyTeams = null,
        bool isMultiDepotRecommended = false,
        int? clusterId = null,
        CancellationToken cancellationToken = default);

    Task<RescueMissionSuggestionResult> PreviewSuggestionAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots,
        List<AgentTeamInfo>? nearbyTeams,
        bool isMultiDepotRecommended,
        int clusterId,
        PromptModel promptOverride,
        AiConfigModel? aiConfigOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the AI generation process as SSE events:
    /// "status" → progress messages, "chunk" → raw AI text tokens, "result" → final parsed result.
    /// </summary>
    IAsyncEnumerable<SseMissionEvent> GenerateSuggestionStreamAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots = null,
        List<AgentTeamInfo>? nearbyTeams = null,
        bool isMultiDepotRecommended = false,
        int? clusterId = null,
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
    public decimal Capacity { get; set; }
    public decimal CurrentUtilization { get; set; }
    public decimal WeightCapacity { get; set; }
    public decimal CurrentWeightUtilization { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>Danh sách vật phẩm còn khả dụng (quantity - reserved > 0) trong kho này.</summary>
    public List<DepotInventoryItemDto> Inventories { get; set; } = [];
}

/// <summary>Một dòng vật phẩm khả dụng trong kho tiếp tế.</summary>
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
    public string? LatestIncidentNote { get; set; }
    public List<string> IncidentNotes { get; set; } = [];
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? TargetVictimSummary { get; set; }
    public List<MissionActivityTargetVictimDto> TargetVictims { get; set; } = [];
}

public class RescueMissionSuggestionResult
{
    public int? SuggestionId { get; set; }
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
    public string MixedRescueReliefWarning { get; set; } = string.Empty;
    /// <summary>true khi coordinator cần bổ sung thêm kho/nguồn cấp phát vì kho được chọn chưa đủ đồ.</summary>
    public bool NeedsAdditionalDepot { get; set; }
    /// <summary>Danh sách vật phẩm còn thiếu sau khi đối chiếu với kho phù hợp nhất mà AI đã chọn cho mission.</summary>
    public List<SupplyShortageDto> SupplyShortages { get; set; } = [];
    public double ConfidenceScore { get; set; }
    public string? RawAiResponse { get; set; }

    /// <summary>true khi AI có độ tự tin thấp - cần người điều phối xem xét thủ công.</summary>
    public bool NeedsManualReview { get; set; }
    /// <summary>Thông báo giải thích lý do cần xem xét thủ công (chỉ set khi NeedsManualReview = true).</summary>
    public string? LowConfidenceWarning { get; set; }
    /// <summary>true khi không có kho nào đủ đa dạng hàng để cấp phát trong một lần - AI sẽ được nhắc lấy từ nhiều kho.</summary>
    public bool MultiDepotRecommended { get; set; }

    /// <summary>Đội cứu hộ được AI đề xuất cho sứ mệnh này (populated khi agent tìm được đội phù hợp).</summary>
    public SuggestedTeamDto? SuggestedTeam { get; set; }

    public MissionSuggestionPipelineMetadata? PipelineMetadata { get; set; }
}

public class SupplyShortageDto
{
    /// <summary>ID SOS chịu ảnh hưởng trực tiếp bởi thiếu hụt này.</summary>
    public int? SosRequestId { get; set; }
    /// <summary>ID vật phẩm nếu backend/AI xác định được từ inventory.</summary>
    public int? ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    /// <summary>ID kho mà AI đã chọn làm kho chính cho mission, nếu có.</summary>
    public int? SelectedDepotId { get; set; }
    public string? SelectedDepotName { get; set; }
    public int NeededQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int MissingQuantity { get; set; }
    public string? Notes { get; set; }
}

public class SupplyToCollectDto
{
    /// <summary>ID của relief item tương ứng trong kho (khớp với DepotInventoryItemDto.ItemId).</summary>
    public int? ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public string? Unit { get; set; }
    /// <summary>Chỉ có trước khi pickup succeed: danh sách các lô FEFO mà activity này phải lấy.</summary>
    public List<SupplyExecutionLotDto>? PlannedPickupLotAllocations { get; set; }
    /// <summary>Chỉ có trước khi pickup succeed: danh sách reusable units hoặc serial phải lấy cho activity này.</summary>
    public List<SupplyExecutionReusableUnitDto>? PlannedPickupReusableUnits { get; set; }
    /// <summary>Chỉ có sau khi pickup succeed: consumable thực tế đã lấy từ các lô nào theo FEFO.</summary>
    public List<SupplyExecutionLotDto>? PickupLotAllocations { get; set; }
    /// <summary>Chỉ có sau khi pickup succeed: reusable units thực tế đã lấy khỏi kho.</summary>
    public List<SupplyExecutionReusableUnitDto>? PickedReusableUnits { get; set; }
    public List<SupplyExecutionLotDto>? AvailableDeliveryLotAllocations { get; set; }
    public List<SupplyExecutionReusableUnitDto>? AvailableDeliveryReusableUnits { get; set; }
    public List<SupplyExecutionLotDto>? DeliveredLotAllocations { get; set; }
    public List<SupplyExecutionReusableUnitDto>? DeliveredReusableUnits { get; set; }
    public List<SupplyExecutionLotDto>? ExpectedReturnLotAllocations { get; set; }
    /// <summary>Chỉ có với RETURN_SUPPLIES: tập reusable units dự kiến phải trả lại kho.</summary>
    public List<SupplyExecutionReusableUnitDto>? ExpectedReturnUnits { get; set; }
    public List<SupplyExecutionLotDto>? ReturnedLotAllocations { get; set; }
    /// <summary>Chỉ có sau khi depot manager confirm return: reusable units thực tế đã nhận lại.</summary>
    public List<SupplyExecutionReusableUnitDto>? ReturnedReusableUnits { get; set; }
    /// <summary>Chỉ có sau khi depot manager confirm return: số lượng thực tế được nhập lại cho item này.</summary>
    public int? ActualReturnedQuantity { get; set; }
    /// <summary>Tỉ lệ dự trù buffer so với số lượng cần thiết (ví dụ: 0.10 = 10%). Được tính khi tạo mission.</summary>
    public double? BufferRatio { get; set; }
    /// <summary>Số lượng dự trù buffer được tính toán: CEIL(Quantity × BufferRatio). Được reserve upfront trong kho.</summary>
    public int? BufferQuantity { get; set; }
    /// <summary>Số lượng buffer thực tế đã sử dụng khi lấy hàng. Chỉ set khi gọi confirm-pickup với buffer usage.</summary>
    public int? BufferUsedQuantity { get; set; }
    /// <summary>Lý do sử dụng buffer - bắt buộc khi BufferUsedQuantity > 0.</summary>
    public string? BufferUsedReason { get; set; }
    /// <summary>Chỉ có sau khi team confirm delivery: số lượng thực tế đã giao tới điểm đích cho item này.</summary>
    public int? ActualDeliveredQuantity { get; set; }
}

public class SuggestedActivityDto
{
    public int Step { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? TargetVictimSummary { get; set; }
    public List<MissionActivityTargetVictimDto> TargetVictims { get; set; } = [];
    public string? Priority { get; set; }
    public string? EstimatedTime { get; set; }
    /// <summary>`SingleTeam` nếu một đội tự hoàn thành được, `SplitAcrossTeams` nếu đây là một nhánh trong kế hoạch nhiều đội.</summary>
    public string? ExecutionMode { get; set; }
    /// <summary>Số đội tối thiểu cần cho mục tiêu thực tế mà activity này thuộc về.</summary>
    public int? RequiredTeamCount { get; set; }
    /// <summary>Khoá nhóm để nối các activity khác nhau nhưng cùng thuộc một kế hoạch phối hợp nhiều đội.</summary>
    public string? CoordinationGroupKey { get; set; }
    /// <summary>Giải thích vì sao activity này là single-team hay là một phần của kế hoạch nhiều đội.</summary>
    public string? CoordinationNotes { get; set; }
    /// <summary>ID của SOS request mà activity này phục vụ trực tiếp.</summary>
    public int? SosRequestId { get; set; }
    /// <summary>ID kho tiếp tế (chỉ có khi ActivityType = COLLECT_SUPPLIES hoặc DELIVER_SUPPLIES)</summary>
    public int? DepotId { get; set; }
    /// <summary>Tên kho tiếp tế</summary>
    public string? DepotName { get; set; }
    /// <summary>Địa chỉ kho tiếp tế</summary>
    public string? DepotAddress { get; set; }
    /// <summary>ID điểm tập kết gần nhất được dùng cho RESCUE/EVACUATE activity.</summary>
    public int? AssemblyPointId { get; set; }
    public string? AssemblyPointName { get; set; }
    public double? AssemblyPointLatitude { get; set; }
    public double? AssemblyPointLongitude { get; set; }
    /// <summary>Tên điểm đến (kho hoặc điểm tập kết) - ưu tiên hiển thị thay cho tọa độ thô.</summary>
    public string? DestinationName { get; set; }
    /// <summary>Vĩ độ điểm đến của activity (kho, vị trí SOS, hoặc điểm tập kết). Frontend dùng để hiển thị bản đồ.</summary>
    public double? DestinationLatitude { get; set; }
    /// <summary>Kinh độ điểm đến của activity. Frontend dùng để hiển thị bản đồ.</summary>
    public double? DestinationLongitude { get; set; }
    /// <summary>Danh sách vật phẩm cần lấy/giao</summary>
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
    /// <summary>ID điểm tập kết của đội (nếu có).</summary>
    public int? AssemblyPointId { get; set; }
    /// <summary>Tên điểm tập kết của đội (nếu có).</summary>
    public string? AssemblyPointName { get; set; }
    /// <summary>Vĩ độ điểm tập kết / vị trí đội.</summary>
    public double? Latitude { get; set; }
    /// <summary>Kinh độ điểm tập kết / vị trí đội.</summary>
    public double? Longitude { get; set; }
    /// <summary>Khoảng cách (km) từ điểm tập kết của đội tới tâm cluster hiện tại nếu backend xác định được.</summary>
    public double? DistanceKm { get; set; }
}

/// <summary>Thông tin vật phẩm trong kho trả về bởi searchInventory tool.</summary>
public class AgentInventoryItem
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public string? Unit { get; set; }
    /// <summary>Số lượng khả dụng thực tế: quantity cho Consumable, số đơn vị sẵn sàng cho Reusable.</summary>
    public int AvailableQuantity { get; set; }
    /// <summary>Chỉ có với Reusable: số đơn vị condition Good đang Available.</summary>
    public int? GoodAvailableCount { get; set; }
    /// <summary>Chỉ có với Reusable: số đơn vị condition Fair đang Available.</summary>
    public int? FairAvailableCount { get; set; }
    /// <summary>Chỉ có với Reusable: số đơn vị condition Poor đang Available.</summary>
    public int? PoorAvailableCount { get; set; }
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
    /// <summary>ID điểm tập kết của đội.</summary>
    public int? AssemblyPointId { get; set; }
    /// <summary>Tên điểm tập kết của đội.</summary>
    public string? AssemblyPointName { get; set; }
    /// <summary>Vĩ độ điểm tập kết.</summary>
    public double? Latitude { get; set; }
    /// <summary>Kinh độ điểm tập kết.</summary>
    public double? Longitude { get; set; }
    /// <summary>Khoảng cách (km) từ điểm tập kết của đội tới cluster; dùng để AI chỉ chọn các đội gần.</summary>
    public double? DistanceKm { get; set; }
}

/// <summary>Thông tin điểm tập kết trả về bởi getAssemblyPoints tool.</summary>
public class AgentAssemblyPointInfo
{
    public int AssemblyPointId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int MaxCapacity { get; set; }
}
