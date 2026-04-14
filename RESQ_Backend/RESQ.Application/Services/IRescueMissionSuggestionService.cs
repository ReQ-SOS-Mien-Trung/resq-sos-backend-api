using RESQ.Application.Common.Models;

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

    /// <summary>
    /// Streams the AI generation process as SSE events:
    /// "status" ? progress messages, "chunk" ? raw AI text tokens, "result" ? final parsed result.
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

/// <summary>Thông tin tóm t?t kho ti?p t? g?n nh?t, dùng d? cung c?p context cho AI.</summary>
public class DepotSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    /// <summary>Kho?ng cách (km) t? kho d?n SOS request quan tr?ng nh?t trong cluster.</summary>
    public double DistanceKm { get; set; }
    public decimal Capacity { get; set; }
    public decimal CurrentUtilization { get; set; }
    public decimal WeightCapacity { get; set; }
    public decimal CurrentWeightUtilization { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>Danh sách v?t ph?m còn kh? d?ng (quantity - reserved > 0) trong kho này.</summary>
    public List<DepotInventoryItemDto> Inventories { get; set; } = [];
}

/// <summary>M?t dòng v?t ph?m kh? d?ng trong kho ti?p t?.</summary>
public class DepotInventoryItemDto
{
    /// <summary>ID c?a relief item trong DB (dùng d? AI tr? v? item_id trong supplies_to_collect).</summary>
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
    /// <summary>true khi coordinator c?n b? sung thêm kho/ngu?n c?p phát vì kho du?c ch?n chua d? d?.</summary>
    public bool NeedsAdditionalDepot { get; set; }
    /// <summary>Danh sách v?t ph?m còn thi?u sau khi d?i chi?u v?i kho phù h?p nh?t mà AI dã ch?n cho mission.</summary>
    public List<SupplyShortageDto> SupplyShortages { get; set; } = [];
    public double ConfidenceScore { get; set; }
    public string? RawAiResponse { get; set; }

    /// <summary>true khi AI có d? t? tin th?p - c?n ngu?i di?u ph?i xem xét th? công.</summary>
    public bool NeedsManualReview { get; set; }
    /// <summary>Thông báo gi?i thích lý do c?n xem xét th? công (ch? set khi NeedsManualReview = true).</summary>
    public string? LowConfidenceWarning { get; set; }
    /// <summary>true khi không có kho nào d? da d?ng hàng d? c?p phát trong m?t l?n - AI s? du?c nh?c l?y t? nhi?u kho.</summary>
    public bool MultiDepotRecommended { get; set; }

    /// <summary>Ð?i c?u h? du?c AI d? xu?t cho s? m?nh này (populated khi agent tìm du?c d?i phù h?p).</summary>
    public SuggestedTeamDto? SuggestedTeam { get; set; }
}

public class SupplyShortageDto
{
    /// <summary>ID SOS ch?u ?nh hu?ng tr?c ti?p b?i thi?u h?t này.</summary>
    public int? SosRequestId { get; set; }
    /// <summary>ID v?t ph?m n?u backend/AI xác d?nh du?c t? inventory.</summary>
    public int? ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    /// <summary>ID kho mà AI dã ch?n làm kho chính cho mission, n?u có.</summary>
    public int? SelectedDepotId { get; set; }
    public string? SelectedDepotName { get; set; }
    public int NeededQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int MissingQuantity { get; set; }
    public string? Notes { get; set; }
}

public class SupplyToCollectDto
{
    /// <summary>ID c?a relief item tuong ?ng trong kho (kh?p v?i DepotInventoryItemDto.ItemId).</summary>
    public int? ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public string? Unit { get; set; }
    /// <summary>Ch? có tru?c khi pickup succeed: danh sách các lô FEFO mà activity này ph?i l?y.</summary>
    public List<SupplyExecutionLotDto>? PlannedPickupLotAllocations { get; set; }
    /// <summary>Ch? có tru?c khi pickup succeed: danh sách reusable units ho?c serial ph?i l?y cho activity này.</summary>
    public List<SupplyExecutionReusableUnitDto>? PlannedPickupReusableUnits { get; set; }
    /// <summary>Ch? có sau khi pickup succeed: consumable th?c t? dã l?y t? các lô nào theo FEFO.</summary>
    public List<SupplyExecutionLotDto>? PickupLotAllocations { get; set; }
    /// <summary>Ch? có sau khi pickup succeed: reusable units th?c t? dã l?y kh?i kho.</summary>
    public List<SupplyExecutionReusableUnitDto>? PickedReusableUnits { get; set; }
    /// <summary>Ch? có v?i RETURN_SUPPLIES: t?p reusable units d? ki?n ph?i tr? l?i kho.</summary>
    public List<SupplyExecutionReusableUnitDto>? ExpectedReturnUnits { get; set; }
    /// <summary>Ch? có sau khi depot manager confirm return: reusable units th?c t? dã nh?n l?i.</summary>
    public List<SupplyExecutionReusableUnitDto>? ReturnedReusableUnits { get; set; }
    /// <summary>Ch? có sau khi depot manager confirm return: s? lu?ng th?c t? du?c nh?p l?i cho item này.</summary>
    public int? ActualReturnedQuantity { get; set; }
    /// <summary>T? l? d? trù buffer so v?i s? lu?ng c?n thi?t (ví d?: 0.10 = 10%). Ðu?c tính khi t?o mission.</summary>
    public double? BufferRatio { get; set; }
    /// <summary>S? lu?ng d? trù buffer du?c tính toán: CEIL(Quantity × BufferRatio). Ðu?c reserve upfront trong kho.</summary>
    public int? BufferQuantity { get; set; }
    /// <summary>S? lu?ng buffer th?c t? dã s? d?ng khi l?y hàng. Ch? set khi g?i confirm-pickup v?i buffer usage.</summary>
    public int? BufferUsedQuantity { get; set; }
    /// <summary>Lý do s? d?ng buffer - b?t bu?c khi BufferUsedQuantity > 0.</summary>
    public string? BufferUsedReason { get; set; }
    /// <summary>Ch? có sau khi team confirm delivery: s? lu?ng th?c t? dã giao t?i di?m dích cho item này.</summary>
    public int? ActualDeliveredQuantity { get; set; }
}

public class SuggestedActivityDto
{
    public int Step { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Priority { get; set; }
    public string? EstimatedTime { get; set; }
    /// <summary>`SingleTeam` n?u m?t d?i t? hoàn thành du?c, `SplitAcrossTeams` n?u dây là m?t nhánh trong k? ho?ch nhi?u d?i.</summary>
    public string? ExecutionMode { get; set; }
    /// <summary>S? d?i t?i thi?u c?n cho m?c tiêu th?c t? mà activity này thu?c v?.</summary>
    public int? RequiredTeamCount { get; set; }
    /// <summary>Khoá nhóm d? n?i các activity khác nhau nhung cùng thu?c m?t k? ho?ch ph?i h?p nhi?u d?i.</summary>
    public string? CoordinationGroupKey { get; set; }
    /// <summary>Gi?i thích vì sao activity này là single-team hay là m?t ph?n c?a k? ho?ch nhi?u d?i.</summary>
    public string? CoordinationNotes { get; set; }
    /// <summary>ID c?a SOS request mà activity này ph?c v? tr?c ti?p.</summary>
    public int? SosRequestId { get; set; }
    /// <summary>ID kho ti?p t? (ch? có khi ActivityType = COLLECT_SUPPLIES ho?c DELIVER_SUPPLIES)</summary>
    public int? DepotId { get; set; }
    /// <summary>Tên kho ti?p t?</summary>
    public string? DepotName { get; set; }
    /// <summary>Ð?a ch? kho ti?p t?</summary>
    public string? DepotAddress { get; set; }
    /// <summary>ID di?m t?p k?t g?n nh?t du?c dùng cho RESCUE/EVACUATE activity.</summary>
    public int? AssemblyPointId { get; set; }
    public string? AssemblyPointName { get; set; }
    public double? AssemblyPointLatitude { get; set; }
    public double? AssemblyPointLongitude { get; set; }
    /// <summary>Tên di?m d?n (kho ho?c di?m t?p k?t) - uu tiên hi?n th? thay cho t?a d? thô.</summary>
    public string? DestinationName { get; set; }
    /// <summary>Vi d? di?m d?n c?a activity (kho, v? trí SOS, ho?c di?m t?p k?t). Frontend dùng d? hi?n th? b?n d?.</summary>
    public double? DestinationLatitude { get; set; }
    /// <summary>Kinh d? di?m d?n c?a activity. Frontend dùng d? hi?n th? b?n d?.</summary>
    public double? DestinationLongitude { get; set; }
    /// <summary>Danh sách v?t ph?m c?n l?y/giao</summary>
    public List<SupplyToCollectDto>? SuppliesToCollect { get; set; }
    /// <summary>Ð?i c?u h? du?c AI giao th?c hi?n activity này.</summary>
    public SuggestedTeamDto? SuggestedTeam { get; set; }
}

public class SuggestedResourceDto
{
    public string ResourceType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? Quantity { get; set; }
    public string? Priority { get; set; }
}

/// <summary>Ð?i c?u h? du?c AI d? xu?t th?c hi?n s? m?nh.</summary>
public class SuggestedTeamDto
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamType { get; set; }
    public string? Reason { get; set; }
    /// <summary>ID di?m t?p k?t c?a d?i (n?u có).</summary>
    public int? AssemblyPointId { get; set; }
    /// <summary>Tên di?m t?p k?t c?a d?i (n?u có).</summary>
    public string? AssemblyPointName { get; set; }
    /// <summary>Vi d? di?m t?p k?t / v? trí d?i.</summary>
    public double? Latitude { get; set; }
    /// <summary>Kinh d? di?m t?p k?t / v? trí d?i.</summary>
    public double? Longitude { get; set; }
    /// <summary>Kho?ng cách (km) t? di?m t?p k?t c?a d?i t?i tâm cluster hi?n t?i n?u backend xác d?nh du?c.</summary>
    public double? DistanceKm { get; set; }
}

/// <summary>Thông tin v?t ph?m trong kho tr? v? b?i searchInventory tool.</summary>
public class AgentInventoryItem
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public string? Unit { get; set; }
    /// <summary>S? lu?ng kh? d?ng th?c t?: quantity cho Consumable, s? don v? s?n sàng cho Reusable.</summary>
    public int AvailableQuantity { get; set; }
    /// <summary>Ch? có v?i Reusable: s? don v? condition Good dang Available.</summary>
    public int? GoodAvailableCount { get; set; }
    /// <summary>Ch? có v?i Reusable: s? don v? condition Fair dang Available.</summary>
    public int? FairAvailableCount { get; set; }
    /// <summary>Ch? có v?i Reusable: s? don v? condition Poor dang Available.</summary>
    public int? PoorAvailableCount { get; set; }
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public string? DepotAddress { get; set; }
    /// <summary>Vi d? kho ch?a.</summary>
    public double? DepotLatitude { get; set; }
    /// <summary>Kinh d? kho ch?a.</summary>
    public double? DepotLongitude { get; set; }
}

/// <summary>Thông tin d?i c?u h? tr? v? b?i getTeams tool.</summary>
public class AgentTeamInfo
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamType { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public int MemberCount { get; set; }
    /// <summary>ID di?m t?p k?t c?a d?i.</summary>
    public int? AssemblyPointId { get; set; }
    /// <summary>Tên di?m t?p k?t c?a d?i.</summary>
    public string? AssemblyPointName { get; set; }
    /// <summary>Vi d? di?m t?p k?t.</summary>
    public double? Latitude { get; set; }
    /// <summary>Kinh d? di?m t?p k?t.</summary>
    public double? Longitude { get; set; }
    /// <summary>Kho?ng cách (km) t? di?m t?p k?t c?a d?i t?i cluster; dùng d? AI ch? ch?n các d?i g?n.</summary>
    public double? DistanceKm { get; set; }
}

/// <summary>Thông tin di?m t?p k?t tr? v? b?i getAssemblyPoints tool.</summary>
public class AgentAssemblyPointInfo
{
    public int AssemblyPointId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int MaxCapacity { get; set; }
}
