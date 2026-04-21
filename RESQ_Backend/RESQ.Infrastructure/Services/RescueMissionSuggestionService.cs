using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RESQ.Application.Common;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.Personnel;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Options;

namespace RESQ.Infrastructure.Services;

public partial class RescueMissionSuggestionService : IRescueMissionSuggestionService
{
    private readonly IAiProviderClientFactory _aiProviderClientFactory;
    private readonly IAiPromptExecutionSettingsResolver _settingsResolver;
    private readonly IAiConfigRepository _aiConfigRepository;
    private readonly IPromptRepository _promptRepository;
    private readonly IMissionAiSuggestionRepository _missionAiSuggestionRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository;
    private readonly IAssemblyPointRepository _assemblyPointRepository;
    private readonly MissionSuggestionPipelineOptions _pipelineOptions;
    private readonly ILogger<RescueMissionSuggestionService> _logger;

    private const double LowConfidenceThreshold = 0.65;

    private const int MaxAgentTurns = 20;
    private const int AgentPageSize = 10;
    private const string CollectSuppliesActivityType = "COLLECT_SUPPLIES";
    private const string ReturnSuppliesActivityType = "RETURN_SUPPLIES";
    private const string ReturnAssemblyPointActivityType = "RETURN_ASSEMBLY_POINT";
    private const string ReusableItemType = "Reusable";
    private const string SingleTeamExecutionMode = "SingleTeam";
    private const string DefaultReturnAssemblyEstimatedTime = "20 phút";

    private static readonly string[] OnSiteActivityTypes = ["DELIVER_SUPPLIES", "RESCUE", "MEDICAL_AID", "EVACUATE"];
    private static readonly Regex SosIdRegex = new(@"SOS\s*ID\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CoordinateRegex = new(@"(-?\d{1,3}\.\d+)\s*,\s*(-?\d{1,3}\.\d+)", RegexOptions.Compiled);
    private sealed record MissionSuggestionExecutionOptions(
        bool PersistSuggestion,
        PromptModel? PromptOverride,
        AiConfigModel? AiConfigOverride)
    {
        public static readonly MissionSuggestionExecutionOptions Persisted = new(true, null, null);

        public static MissionSuggestionExecutionOptions Preview(
            PromptModel promptOverride,
            AiConfigModel? aiConfigOverride) =>
            new(false, promptOverride, aiConfigOverride);
    }

    private sealed record MissionExecutionAssessment(
        bool HasExecutableActivities,
        bool RequiresMixedBranches,
        bool HasRescueBranch,
        bool HasReliefBranch,
        string? FailureReason)
    {
        public bool IsExecutable =>
            HasExecutableActivities
            && (!RequiresMixedBranches || (HasRescueBranch && HasReliefBranch));
    }

    private sealed record MissionResultSelection(
        RescueMissionSuggestionResult Result,
        string FinalResultSource);

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public RescueMissionSuggestionService(
        IAiProviderClientFactory aiProviderClientFactory,
        IAiPromptExecutionSettingsResolver settingsResolver,
        IAiConfigRepository aiConfigRepository,
        IPromptRepository promptRepository,
        IMissionAiSuggestionRepository missionAiSuggestionRepository,
        IDepotInventoryRepository depotInventoryRepository,
        IItemModelMetadataRepository itemModelMetadataRepository,
        IAssemblyPointRepository assemblyPointRepository,
        IOptions<MissionSuggestionPipelineOptions> pipelineOptions,
        ILogger<RescueMissionSuggestionService> logger)
    {
        _aiProviderClientFactory = aiProviderClientFactory;
        _settingsResolver = settingsResolver;
        _aiConfigRepository = aiConfigRepository;
        _promptRepository = promptRepository;
        _missionAiSuggestionRepository = missionAiSuggestionRepository;
        _depotInventoryRepository = depotInventoryRepository;
        _itemModelMetadataRepository = itemModelMetadataRepository;
        _assemblyPointRepository = assemblyPointRepository;
        _pipelineOptions = pipelineOptions.Value;
        _logger = logger;
    }

    public Task<RescueMissionSuggestionResult> GenerateSuggestionAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots = null,
        List<AgentTeamInfo>? nearbyTeams = null,
        bool isMultiDepotRecommended = false,
        int? clusterId = null,
        CancellationToken cancellationToken = default) =>
        RunSuggestionAsync(
            sosRequests,
            nearbyDepots,
            nearbyTeams,
            isMultiDepotRecommended,
            clusterId,
            MissionSuggestionExecutionOptions.Persisted,
            cancellationToken);

    public Task<RescueMissionSuggestionResult> PreviewSuggestionAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots,
        List<AgentTeamInfo>? nearbyTeams,
        bool isMultiDepotRecommended,
        int clusterId,
        PromptModel promptOverride,
        AiConfigModel? aiConfigOverride = null,
        CancellationToken cancellationToken = default) =>
        RunSuggestionAsync(
            sosRequests,
            nearbyDepots,
            nearbyTeams,
            isMultiDepotRecommended,
            clusterId,
            MissionSuggestionExecutionOptions.Preview(promptOverride, aiConfigOverride),
            cancellationToken);

    private async Task<RescueMissionSuggestionResult> RunSuggestionAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots,
        List<AgentTeamInfo>? nearbyTeams,
        bool isMultiDepotRecommended,
        int? clusterId,
        MissionSuggestionExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        RescueMissionSuggestionResult? finalResult = null;

        try
        {
            await foreach (var evt in GenerateSuggestionStreamCoreAsync(
                sosRequests,
                nearbyDepots,
                nearbyTeams,
                isMultiDepotRecommended,
                clusterId,
                options,
                cancellationToken))
            {
                if (evt.EventType == "result" && evt.Result != null)
                    finalResult = evt.Result;
                else if (evt.EventType == "error")
                {
                    stopwatch.Stop();
                    return new RescueMissionSuggestionResult
                    {
                        IsSuccess = false,
                        ErrorMessage = evt.Data,
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error generating rescue mission suggestion");
            return new RescueMissionSuggestionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Lỗi khi gọi AI: {ex.Message}",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }

        stopwatch.Stop();
        if (finalResult != null)
        {
            finalResult.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            if (!options.PersistSuggestion)
                finalResult.SuggestionId = null;

            return finalResult;
        }

        return new RescueMissionSuggestionResult
        {
            IsSuccess = false,
            ErrorMessage = "AI không phản hồi. Vui lòng thử lại sau.",
            ResponseTimeMs = stopwatch.ElapsedMilliseconds
        };
    }
    private static string BuildSosRequestsData(List<SosRequestSummary> sosRequests)
    {
        var now = DateTime.UtcNow;
        var entries = sosRequests.Select((sos, index) =>
        {
            var victimContext = ResolveVictimContext(sos);
            return new
            {
                stt = index + 1,
                id = sos.Id,
                loai_sos = sos.SosType ?? "Không xác định",
                tin_nhan = sos.RawMessage,
                du_lieu_chi_tiet = sos.StructuredData ?? "Không có",
                muc_uu_tien = sos.PriorityLevel ?? "Chưa đánh giá",
                ai_analysis = sos.AiAnalysis is null ? null : new
                {
                    has_ai_analysis = sos.AiAnalysis.HasAiAnalysis,
                    suggested_priority = sos.AiAnalysis.SuggestedPriority,
                    suggested_severity = sos.AiAnalysis.SuggestedSeverity,
                    needs_immediate_safe_transfer = sos.AiAnalysis.NeedsImmediateSafeTransfer,
                    can_wait_for_combined_mission = sos.AiAnalysis.CanWaitForCombinedMission,
                    handling_reason = sos.AiAnalysis.HandlingReason
                },
                trang_thai = sos.Status ?? "Không rõ",
                ghi_chu_su_co_moi_nhat = sos.LatestIncidentNote,
                lich_su_su_co = sos.IncidentNotes,
                doi_tuong_can_ho_tro = victimContext.Summary,
                danh_sach_nan_nhan = victimContext.Victims.Select(victim => new
                {
                    ten = victim.DisplayName,
                    loai = victim.PersonType,
                    muc_do = victim.Severity,
                    bi_thuong = victim.IsInjured,
                    van_de_y_te = victim.MedicalIssues,
                    so_dien_thoai = victim.PersonPhone
                }),
                vi_tri = sos.Latitude.HasValue && sos.Longitude.HasValue
                    ? $"{sos.Latitude}, {sos.Longitude}"
                    : "Không xác định",
                thoi_gian_cho_doi_phut = sos.CreatedAt.HasValue
                    ? (int)(now - sos.CreatedAt.Value).TotalMinutes
                    : (int?)null,
                thoi_gian_tao = sos.CreatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"
            };
        });

        return JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static string BuildAgentInstructions(bool isMultiDepotRecommended = false)
    {
        _ = isMultiDepotRecommended;

        return """
            ## HƯỚNG DẪN SỬ DỤNG CÔNG CỤ
            Bạn có thể gọi ba công cụ để lấy dữ liệu thực trước khi lập kế hoạch:

            - **searchInventory(category, type?, page)**: Tìm vật phẩm khả dụng trong **các kho hợp lệ của cluster hiện tại**. Kết quả chỉ chứa các kho backend đã cho phép trong phạm vi lập kế hoạch này. Mỗi dòng là một cặp (vật phẩm, kho) với item_id, item_name, item_type, available_quantity, depot_id, depot_name, depot_address, depot_latitude, depot_longitude. Công cụ này bao gồm cả consumable, reusable, vehicle và các phương tiện/thiết bị nếu tồn kho có sẵn.
            - **getTeams(ability?, available?, page)**: Trả về nearby teams đang Available trong bán kính cluster hiện tại.
            - **getAssemblyPoints(page)**: Trả về các assembly point đang hoạt động.

            ## QUY TẮC KHO — CHỈ CHỌN MỘT KHO CHO TOÀN BỘ MISSION
            - BẮT BUỘC gọi **searchInventory** cho từng danh mục phù hợp: Thực phẩm, Nước, Y tế, Cứu hộ, Quần áo, Sưởi ấm, nơi trú ẩn... Không bỏ sót danh mục liên quan.
            - Có thể dùng các từ khoá nghiệp vụ tổng quát như `Thuốc men`, `Sơ cứu`, `Chăn màn`, `Giữ ấm`; backend sẽ tự map sang nhóm vật phẩm/kho liên quan để tìm item thực tế trong kho.
            - Nếu mission cần phương tiện di chuyển, xe tải, xuồng, ca nô, càng, máy phát, hoặc bất kỳ reusable equipment nào, bắt buộc phải gọi `searchInventory` cho nhóm phương tiện/thiết bị hữu hình trước khi quyết định.
            - Sau khi có kết quả, so sánh các `depot_id` xuất hiện và chọn **đúng một kho phù hợp nhất cho toàn bộ mission**.
            - Tiêu chí chọn kho: ưu tiên kho đáp ứng được nhiều nhu cầu SOS nhất và có tổng số lượng phù hợp cao nhất. Nếu tương đương, chọn kho có vị trí thuận lợi hơn trong kết quả đã trả về.
            - Toàn bộ activity có dùng kho trong mission này phải dùng cùng một `depot_id`, `depot_name`, `depot_address` của kho đã chọn.
            - **TUYỆT ĐỐI KHÔNG** tạo kế hoạch lấy vật phẩm từ kho thứ hai, không chia vật phẩm giữa nhiều kho, không gộp nhiều kho.
            - Nếu kho đã chọn không đủ đồ, vẫn chỉ lấy những gì kho đó hiện có rồi báo thiếu. Không được chuyển sang kho khác.
            - Đây chỉ là bước AI suggestion. Không được giả định tồn kho đã bị reserve; reserve thật chỉ xảy ra khi coordinator tạo mission.

            ## BÁO CÁO THIẾU HỤT vật phẩm
            - Nếu sau khi đối chiếu với kho đã chọn mà còn thiếu bất kỳ vật phẩm nào, đặt `needs_additional_depot = true`.
            - Khi có thiếu hụt, điền `supply_shortages` với từng dòng thiếu theo format:
              - `sos_request_id`: SOS bị ảnh hưởng
              - `item_id`: nếu xác định được từ inventory; nếu không thì để null
              - `item_name`, `unit`
              - `selected_depot_id`, `selected_depot_name`: chính là kho duy nhất đã chọn
              - `needed_quantity`, `available_quantity`, `missing_quantity`
              - `notes`: mô tả ngắn gọn lý do thiếu nếu cần
            - Nếu kho đã chọn không có món đó, dùng `available_quantity = 0` và `missing_quantity = needed_quantity`.
            - Nếu kho chỉ có một phần, dùng `available_quantity < needed_quantity` và `missing_quantity = needed_quantity - available_quantity`.
            - `special_notes` phải ghi rõ rằng coordinator cần bổ sung thêm kho/nguồn cấp phát vì đang thiếu vật phẩm nào và số lượng thiếu bao nhiêu.
            - Nếu không có thiếu hụt, đặt `needs_additional_depot = false` và `supply_shortages = []`.

            ## QUY TẮC ESTIMATE TIME
            - Mỗi activity phải có `estimated_time` theo đúng một trong hai format: `"X phút"` hoặc `"Y giờ Z phút"`.
            - `estimated_time` phải bao gồm thời gian di chuyển thực địa + thời gian lấy hàng/giao hàng + thời gian xử lý tại hiện trường tương ứng với activity đó.
            - `estimated_duration` là tổng thời gian tuần tự của toàn bộ activities theo đúng thứ tự step trong mission, cũng dùng format `"X phút"` hoặc `"Y giờ Z phút"`.
            - Không để `estimated_time` hoặc `estimated_duration` mơ hồ kiểu `"nhanh"`, `"sớm"`, `"khoảng vài giờ"`.

            ## QUY TẮC THỨ TỰ ACTIVITY
            - `COLLECT_SUPPLIES` phải đứng trước activity hiện trường sử dụng số vật phẩm đó.
            - Không được tạo thêm `COLLECT_SUPPLIES` cho cùng SOS sau khi đã bắt đầu `DELIVER_SUPPLIES`, `RESCUE`, `MEDICAL_AID`, hoặc `EVACUATE` của SOS đó.
            - Nếu có vật phẩm reusable được lấy ở `COLLECT_SUPPLIES`, phải có `RETURN_SUPPLIES` ở cuối kế hoạch để trả đúng về cùng kho đã chọn.
            - Nếu lấy phương tiện/reusable từ kho, phải giữ chúng trong `supplies_to_collect` của `COLLECT_SUPPLIES` và `RETURN_SUPPLIES`. Không đẩy xuống `resources[]` nếu đã map được item trong kho.
            - Không tạo `COLLECT_SUPPLIES` ở cuối kế hoạch nếu phía sau không có activity nào dùng số hàng đó.

            ## QUY TẮC TỪNG LOẠI ACTIVITY
            - `COLLECT_SUPPLIES`: chỉ tạo cho vật phẩm thật sự lấy từ kho đã chọn; `supplies_to_collect` chỉ chứa các item có trong kho đó. Nếu kho có xe/phương tiện/reusable phù hợp thì đưa thẳng vào đây như một inventory-backed item.
            - `DELIVER_SUPPLIES`: giao đúng các vật phẩm vừa lấy từ kho đã chọn cho SOS tương ứng.
            - `RESCUE`: luôn tạo nếu hiện trường cần cứu người, kể cả khi thiết bị cứu hộ bị thiếu; thiếu gì thì ghi vào `supply_shortages` và `special_notes`.
            - `MEDICAL_AID`: nếu thiếu vật phẩm y tế thì vẫn có thể tạo activity, nhưng phải ghi rõ thiếu hụt.
            - `EVACUATE`: không lấy vật phẩm ở bước này; phải chọn `assembly_point_id` gần nạn nhân nhất.
            - `resources[]`: chỉ dùng cho năng lực tổng quát khi không map được thành item tồn kho cụ thể. Nếu kho đã có item phù hợp, ưu tiên hiện nó trong activity lấy đồ.

            ## QUY TẮC TEAM VÀ ASSEMBLY POINT
            - Gọi `getTeams` để lấy `team_id`; không tự bịa team ngoài kết quả công cụ.
            - Nếu lọc theo `ability` mà không thấy team, gọi lại `getTeams` không truyền ability trước khi chấp nhận `suggested_team = null`.
            - Với `RESCUE` hoặc `EVACUATE`, bắt buộc gọi `getAssemblyPoints` và chọn `assembly_point_id` gần nạn nhân nhất.

            ## QUY TẮC AN TOÀN MISSION GHÉP CỨU HỘ + CỨU TRỢ
            - Nếu mission có cả nhánh `RESCUE|EVACUATE|MEDICAL_AID` và nhánh `COLLECT_SUPPLIES|DELIVER_SUPPLIES`, backend sẽ tự thêm cảnh báo an toàn sau khi parse kết quả.
            - Cảnh báo mixed mission không phải là lý do để bỏ trống `activities`. Khi đã trả mission JSON, `activities` phải là execution plan cụ thể.
            - Nếu cluster mixed có rescue khẩn cấp cần tách riêng, vẫn phải trả full mixed route an toàn trong `activities`, đồng thời ghi cảnh báo tách cluster ở `special_notes`.
            - Nếu route mixed hiện tại không an toàn, hãy rewrite lại thứ tự hoạt động. Không được thay route bằng `activities = []`.
            - Không tạo `warnings[]`, không tạo warning code riêng, không chèn warning schema mới vào JSON.

            ## ĐỊNH DẠNG overall_assessment
            - Toàn bộ nội dung phải nằm trên một dòng duy nhất.
            - Khi nhắc tới SOS, dùng format `[SOS ID X]: ...`.

            ## JSON BẮT BUỘC
            - Trả về JSON thuần, không markdown.
            - Ngoài các field mission hiện có, luôn trả thêm:
              - `needs_additional_depot`: boolean
              - `supply_shortages`: array
                        - Dùng `special_notes` để ghi cảnh báo mixed mission nếu có.
            """;
    }

    private static RescueMissionSuggestionResult ParseMissionSuggestion(string response)
    {
        // Step 1: Strip ```json ... ``` markdown fence if present
        var cleaned = response.Trim();
        if (cleaned.StartsWith("```"))
        {
            var fenceEnd = cleaned.IndexOf('\n');
            if (fenceEnd >= 0)
                cleaned = cleaned[(fenceEnd + 1)..];
            var closingFence = cleaned.LastIndexOf("```");
            if (closingFence >= 0)
                cleaned = cleaned[..closingFence];
            cleaned = cleaned.Trim();
        }

        // Step 2: Extract JSON object boundaries
        var jsonStart = cleaned.IndexOf('{');
        var jsonEnd = cleaned.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var jsonStr = cleaned[jsonStart..(jsonEnd + 1)];

            // Step 3: Try full deserialization
            try
            {
                var parsed = JsonSerializer.Deserialize<AiMissionSuggestion>(jsonStr, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                });
                if (parsed != null)
                    return MapParsedToResult(parsed);
            }
            catch { /* fall through to partial extraction */ }

            // Step 4: Partial extraction via JsonDocument (handles valid-but-incomplete structures)
            try
            {
                return ExtractPartialFromJson(jsonStr);
            }
            catch { /* fall through to regex */ }
        }

        // Step 5: Regex extraction for severely truncated responses
        return ExtractViaRegex(cleaned.Length > 0 ? cleaned : response);
    }

    private static RescueMissionSuggestionResult MapParsedToResult(AiMissionSuggestion parsed)
    {
        return new RescueMissionSuggestionResult
        {
            SuggestedMissionTitle = parsed.MissionTitle,
            SuggestedMissionType = parsed.MissionType,
            SuggestedPriorityScore = parsed.PriorityScore > 0 ? parsed.PriorityScore : null,
            SuggestedSeverityLevel = parsed.SeverityLevel,
            OverallAssessment = parsed.OverallAssessment,
            SuggestedActivities = parsed.Activities?.Select(a => new SuggestedActivityDto
            {
                Step = a.Step,
                ActivityType = a.ActivityType ?? string.Empty,
                Description = a.Description ?? string.Empty,
                Priority = a.Priority,
                EstimatedTime = a.EstimatedTime,
                ExecutionMode = a.ExecutionMode,
                RequiredTeamCount = a.RequiredTeamCount,
                CoordinationGroupKey = a.CoordinationGroupKey,
                CoordinationNotes = a.CoordinationNotes,
                SosRequestId = a.SosRequestId,
                DepotId = a.DepotId,
                DepotName = a.DepotName,
                DepotAddress = a.DepotAddress,
                DestinationLatitude  = (a.ActivityType is "COLLECT_SUPPLIES" or "RETURN_SUPPLIES")
                    ? a.DepotLatitude  : a.AssemblyPointLatitude,
                DestinationLongitude = (a.ActivityType is "COLLECT_SUPPLIES" or "RETURN_SUPPLIES")
                    ? a.DepotLongitude : a.AssemblyPointLongitude,
                DestinationName = (a.ActivityType is "COLLECT_SUPPLIES" or "RETURN_SUPPLIES")
                    ? a.DepotName : a.AssemblyPointName,
                SuppliesToCollect = a.SuppliesToCollect?.Select(s => new SupplyToCollectDto
                {
                    ItemId = s.ItemId,
                    ItemName = s.ItemName ?? string.Empty,
                    Quantity = s.Quantity,
                    Unit = s.Unit
                }).ToList(),
                AssemblyPointId = a.AssemblyPointId,
                AssemblyPointName = a.AssemblyPointName,
                AssemblyPointLatitude = a.AssemblyPointLatitude,
                AssemblyPointLongitude = a.AssemblyPointLongitude,
                SuggestedTeam = a.SuggestedTeam == null ? null : new SuggestedTeamDto
                {
                    TeamId            = a.SuggestedTeam.TeamId,
                    TeamName          = a.SuggestedTeam.TeamName ?? string.Empty,
                    TeamType          = a.SuggestedTeam.TeamType,
                    Reason            = a.SuggestedTeam.Reason,
                    AssemblyPointId   = a.SuggestedTeam.AssemblyPointId,
                    AssemblyPointName = a.SuggestedTeam.AssemblyPointName,
                    Latitude          = a.SuggestedTeam.Latitude,
                    Longitude         = a.SuggestedTeam.Longitude,
                    DistanceKm        = a.SuggestedTeam.DistanceKm
                }
            }).ToList() ?? [],
            SuggestedResources = parsed.Resources?.Select(r => new SuggestedResourceDto
            {
                ResourceType = r.ResourceType ?? string.Empty,
                Description = r.Description ?? string.Empty,
                Quantity = r.Quantity,
                Priority = r.Priority
            }).ToList() ?? [],
            SuggestedTeam = parsed.SuggestedTeam == null ? null : new SuggestedTeamDto
            {
                TeamId             = parsed.SuggestedTeam.TeamId,
                TeamName           = parsed.SuggestedTeam.TeamName ?? string.Empty,
                TeamType           = parsed.SuggestedTeam.TeamType,
                Reason             = parsed.SuggestedTeam.Reason,
                AssemblyPointId    = parsed.SuggestedTeam.AssemblyPointId,
                AssemblyPointName  = parsed.SuggestedTeam.AssemblyPointName,
                Latitude           = parsed.SuggestedTeam.Latitude,
                Longitude          = parsed.SuggestedTeam.Longitude,
                DistanceKm         = parsed.SuggestedTeam.DistanceKm
            },
            EstimatedDuration = parsed.EstimatedDuration,
            SpecialNotes = parsed.SpecialNotes,
            NeedsAdditionalDepot = parsed.NeedsAdditionalDepot,
            SupplyShortages = parsed.SupplyShortages?.Select(MapSupplyShortage).ToList() ?? [],
            ConfidenceScore = parsed.ConfidenceScore
        };
    }

    private static RescueMissionSuggestionResult ExtractPartialFromJson(string jsonStr)
    {
        using var doc = JsonDocument.Parse(jsonStr, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var root = doc.RootElement;
        var result = new RescueMissionSuggestionResult();

        if (root.TryGetProperty("mission_title", out var t)) result.SuggestedMissionTitle = t.GetString();
        if (root.TryGetProperty("mission_type", out var mt)) result.SuggestedMissionType = mt.GetString();
        if (root.TryGetProperty("priority_score", out var ps) && ps.TryGetDouble(out var psVal)) result.SuggestedPriorityScore = psVal;
        if (root.TryGetProperty("severity_level", out var sl)) result.SuggestedSeverityLevel = sl.GetString();
        if (root.TryGetProperty("overall_assessment", out var oa)) result.OverallAssessment = oa.GetString()?.Replace("\n", " ").Replace("\r", " ").Trim();
        if (root.TryGetProperty("estimated_duration", out var ed)) result.EstimatedDuration = ed.GetString();
        if (root.TryGetProperty("special_notes", out var sn)) result.SpecialNotes = sn.GetString();
        if (root.TryGetProperty("needs_additional_depot", out var nad) && nad.ValueKind is JsonValueKind.True or JsonValueKind.False) result.NeedsAdditionalDepot = nad.GetBoolean();
        result.SupplyShortages = ParseSupplyShortages(root);
        if (root.TryGetProperty("confidence_score", out var cs) && cs.TryGetDouble(out var csVal)) result.ConfidenceScore = csVal;

        if (root.TryGetProperty("activities", out var acts) && acts.ValueKind == JsonValueKind.Array)
        {
            result.SuggestedActivities = acts.EnumerateArray().Select(a =>
            {
                var dto = new SuggestedActivityDto();
                if (a.TryGetProperty("step", out var sv) && sv.TryGetInt32(out var svi)) dto.Step = svi;
                if (a.TryGetProperty("activity_type", out var at)) dto.ActivityType = at.GetString() ?? string.Empty;
                if (a.TryGetProperty("description", out var d)) dto.Description = d.GetString() ?? string.Empty;
                if (a.TryGetProperty("priority", out var p)) dto.Priority = p.GetString();
                if (a.TryGetProperty("estimated_time", out var et)) dto.EstimatedTime = et.GetString();
                if (a.TryGetProperty("execution_mode", out var em) && em.ValueKind != JsonValueKind.Null) dto.ExecutionMode = em.GetString();
                if (a.TryGetProperty("required_team_count", out var rtc) && rtc.ValueKind != JsonValueKind.Null && rtc.TryGetInt32(out var rtcv)) dto.RequiredTeamCount = rtcv;
                if (a.TryGetProperty("coordination_group_key", out var cgk) && cgk.ValueKind != JsonValueKind.Null) dto.CoordinationGroupKey = cgk.GetString();
                if (a.TryGetProperty("coordination_notes", out var cn) && cn.ValueKind != JsonValueKind.Null) dto.CoordinationNotes = cn.GetString();
                if (a.TryGetProperty("sos_request_id", out var sri) && sri.ValueKind != JsonValueKind.Null && sri.TryGetInt32(out var sriv)) dto.SosRequestId = sriv;
                if (a.TryGetProperty("depot_id", out var di) && di.ValueKind != JsonValueKind.Null && di.TryGetInt32(out var div)) dto.DepotId = div;
                if (a.TryGetProperty("depot_name", out var dn) && dn.ValueKind != JsonValueKind.Null) dto.DepotName = dn.GetString();
                if (a.TryGetProperty("depot_address", out var da) && da.ValueKind != JsonValueKind.Null) dto.DepotAddress = da.GetString();
                if (a.TryGetProperty("depot_latitude",  out var dlat) && dlat.ValueKind != JsonValueKind.Null && dlat.TryGetDouble(out var dlatv)) dto.DestinationLatitude  ??= dlatv;
                if (a.TryGetProperty("depot_longitude", out var dlon) && dlon.ValueKind != JsonValueKind.Null && dlon.TryGetDouble(out var dlonv)) dto.DestinationLongitude ??= dlonv;
                if (!dto.DestinationLatitude.HasValue && a.TryGetProperty("assembly_point_latitude",  out var aplat2) && aplat2.ValueKind != JsonValueKind.Null && aplat2.TryGetDouble(out var aplat2v)) dto.DestinationLatitude  = aplat2v;
                if (!dto.DestinationLongitude.HasValue && a.TryGetProperty("assembly_point_longitude", out var aplon2) && aplon2.ValueKind != JsonValueKind.Null && aplon2.TryGetDouble(out var aplon2v)) dto.DestinationLongitude = aplon2v;
                if (a.TryGetProperty("assembly_point_id", out var api) && api.ValueKind != JsonValueKind.Null && api.TryGetInt32(out var apiv)) dto.AssemblyPointId = apiv;
                if (a.TryGetProperty("assembly_point_name", out var activityApn) && activityApn.ValueKind != JsonValueKind.Null) dto.AssemblyPointName = activityApn.GetString();
                if (a.TryGetProperty("assembly_point_latitude", out var aplat) && aplat.ValueKind != JsonValueKind.Null && aplat.TryGetDouble(out var aplatv)) dto.AssemblyPointLatitude = aplatv;
                if (a.TryGetProperty("assembly_point_longitude", out var aplon) && aplon.ValueKind != JsonValueKind.Null && aplon.TryGetDouble(out var aplonv)) dto.AssemblyPointLongitude = aplonv;
                // DestinationName: prefer depot name for supply activities, assembly point name for rescue/evacuate
                dto.DestinationName ??= dto.DepotName ?? dto.AssemblyPointName;
                if (a.TryGetProperty("supplies_to_collect", out var stc) && stc.ValueKind == JsonValueKind.Array)
                    dto.SuppliesToCollect = stc.EnumerateArray().Select(s =>
                    {
                        var supply = new SupplyToCollectDto();
                        if (s.TryGetProperty("item_id", out var iid) && iid.ValueKind != JsonValueKind.Null && iid.TryGetInt32(out var iidv)) supply.ItemId = iidv;
                        if (s.TryGetProperty("item_name", out var iname)) supply.ItemName = iname.GetString() ?? string.Empty;
                        if (s.TryGetProperty("quantity", out var qty) && qty.TryGetInt32(out var qtyv)) supply.Quantity = qtyv;
                        if (s.TryGetProperty("unit", out var unit) && unit.ValueKind != JsonValueKind.Null) supply.Unit = unit.GetString();
                        return supply;
                    }).ToList();
                if (a.TryGetProperty("suggested_team", out var ast) && ast.ValueKind == JsonValueKind.Object)
                {
                    var teamDto = new SuggestedTeamDto();
                    if (ast.TryGetProperty("team_id",             out var tid)  && tid.TryGetInt32(out var tidv))                                        teamDto.TeamId            = tidv;
                    if (ast.TryGetProperty("team_name",           out var tn)   && tn.ValueKind  != JsonValueKind.Null)                                  teamDto.TeamName          = tn.GetString() ?? string.Empty;
                    if (ast.TryGetProperty("team_type",           out var tt)   && tt.ValueKind  != JsonValueKind.Null)                                  teamDto.TeamType          = tt.GetString();
                    if (ast.TryGetProperty("reason",              out var r)    && r.ValueKind   != JsonValueKind.Null)                                  teamDto.Reason            = r.GetString();
                    if (ast.TryGetProperty("assembly_point_id",   out var apid) && apid.ValueKind != JsonValueKind.Null && apid.TryGetInt32(out var apidv)) teamDto.AssemblyPointId   = apidv;
                    if (ast.TryGetProperty("assembly_point_name", out var apn)  && apn.ValueKind != JsonValueKind.Null)                                  teamDto.AssemblyPointName = apn.GetString();
                    if (ast.TryGetProperty("latitude",            out var lat)  && lat.ValueKind != JsonValueKind.Null && lat.TryGetDouble(out var latv)) teamDto.Latitude          = latv;
                    if (ast.TryGetProperty("longitude",           out var lon)  && lon.ValueKind != JsonValueKind.Null && lon.TryGetDouble(out var lonv)) teamDto.Longitude         = lonv;
                    if (ast.TryGetProperty("distance_km",         out var dkm)  && dkm.ValueKind != JsonValueKind.Null && dkm.TryGetDouble(out var dkmv)) teamDto.DistanceKm        = dkmv;
                    dto.SuggestedTeam = teamDto;
                }
                return dto;
            }).ToList();
        }

        if (root.TryGetProperty("resources", out var ress) && ress.ValueKind == JsonValueKind.Array)
        {
            result.SuggestedResources = ress.EnumerateArray().Select(r =>
            {
                var dto = new SuggestedResourceDto();
                if (r.TryGetProperty("resource_type", out var rt)) dto.ResourceType = rt.GetString() ?? string.Empty;
                if (r.TryGetProperty("description", out var d)) dto.Description = d.GetString() ?? string.Empty;
                if (r.TryGetProperty("quantity", out var q) && q.TryGetInt32(out var qv)) dto.Quantity = qv;
                if (r.TryGetProperty("priority", out var p)) dto.Priority = p.GetString();
                return dto;
            }).ToList();
        }

        if (root.TryGetProperty("suggested_team", out var st) && st.ValueKind == JsonValueKind.Object)
        {
            var teamDto = new SuggestedTeamDto();
            if (st.TryGetProperty("team_id",            out var tid) && tid.TryGetInt32(out var tidv))                                              teamDto.TeamId            = tidv;
            if (st.TryGetProperty("team_name",          out var tn)  && tn.ValueKind  != JsonValueKind.Null)                                        teamDto.TeamName          = tn.GetString() ?? string.Empty;
            if (st.TryGetProperty("team_type",          out var tt)  && tt.ValueKind  != JsonValueKind.Null)                                        teamDto.TeamType          = tt.GetString();
            if (st.TryGetProperty("reason",             out var r)   && r.ValueKind   != JsonValueKind.Null)                                        teamDto.Reason            = r.GetString();
            if (st.TryGetProperty("assembly_point_id",  out var apid) && apid.ValueKind != JsonValueKind.Null && apid.TryGetInt32(out var apidv))    teamDto.AssemblyPointId   = apidv;
            if (st.TryGetProperty("assembly_point_name",out var apn) && apn.ValueKind != JsonValueKind.Null)                                        teamDto.AssemblyPointName = apn.GetString();
            if (st.TryGetProperty("latitude",           out var lat) && lat.ValueKind != JsonValueKind.Null && lat.TryGetDouble(out var latv))       teamDto.Latitude          = latv;
            if (st.TryGetProperty("longitude",          out var lon) && lon.ValueKind != JsonValueKind.Null && lon.TryGetDouble(out var lonv))       teamDto.Longitude         = lonv;
            if (st.TryGetProperty("distance_km",        out var dkm) && dkm.ValueKind != JsonValueKind.Null && dkm.TryGetDouble(out var dkmv))       teamDto.DistanceKm        = dkmv;
            result.SuggestedTeam = teamDto;
        }

        return result;
    }

    private static List<SupplyShortageDto> ParseSupplyShortages(JsonElement root)
    {
        if (!root.TryGetProperty("supply_shortages", out var shortages)
            || shortages.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return shortages.EnumerateArray()
            .Select(shortage =>
            {
                var dto = new SupplyShortageDto();
                if (shortage.TryGetProperty("sos_request_id", out var sri) && sri.ValueKind != JsonValueKind.Null && sri.TryGetInt32(out var sriv)) dto.SosRequestId = sriv;
                if (shortage.TryGetProperty("item_id", out var iid) && iid.ValueKind != JsonValueKind.Null && iid.TryGetInt32(out var iidv)) dto.ItemId = iidv;
                if (shortage.TryGetProperty("item_name", out var itemName) && itemName.ValueKind != JsonValueKind.Null) dto.ItemName = itemName.GetString() ?? string.Empty;
                if (shortage.TryGetProperty("unit", out var unit) && unit.ValueKind != JsonValueKind.Null) dto.Unit = unit.GetString();
                if (shortage.TryGetProperty("selected_depot_id", out var sdi) && sdi.ValueKind != JsonValueKind.Null && sdi.TryGetInt32(out var sdiv)) dto.SelectedDepotId = sdiv;
                if (shortage.TryGetProperty("selected_depot_name", out var sdn) && sdn.ValueKind != JsonValueKind.Null) dto.SelectedDepotName = sdn.GetString();
                if (shortage.TryGetProperty("needed_quantity", out var nq) && nq.ValueKind != JsonValueKind.Null && nq.TryGetInt32(out var nqv)) dto.NeededQuantity = nqv;
                if (shortage.TryGetProperty("available_quantity", out var aq) && aq.ValueKind != JsonValueKind.Null && aq.TryGetInt32(out var aqv)) dto.AvailableQuantity = aqv;
                if (shortage.TryGetProperty("missing_quantity", out var mq) && mq.ValueKind != JsonValueKind.Null && mq.TryGetInt32(out var mqv)) dto.MissingQuantity = mqv;
                if (shortage.TryGetProperty("notes", out var notes) && notes.ValueKind != JsonValueKind.Null) dto.Notes = notes.GetString();
                return dto;
            })
            .Where(shortage => !string.IsNullOrWhiteSpace(shortage.ItemName) || shortage.ItemId.HasValue)
            .ToList();
    }

    private static SupplyShortageDto MapSupplyShortage(AiSupplyShortage shortage)
    {
        return new SupplyShortageDto
        {
            SosRequestId = shortage.SosRequestId,
            ItemId = shortage.ItemId,
            ItemName = shortage.ItemName ?? string.Empty,
            Unit = shortage.Unit,
            SelectedDepotId = shortage.SelectedDepotId,
            SelectedDepotName = shortage.SelectedDepotName,
            NeededQuantity = shortage.NeededQuantity,
            AvailableQuantity = shortage.AvailableQuantity,
            MissingQuantity = shortage.MissingQuantity,
            Notes = shortage.Notes
        };
    }

    private static RescueMissionSuggestionResult ExtractViaRegex(string text)
    {
        static string? ExtractStr(string src, string field)
        {
            var m = Regex.Match(src, $@"""{field}""\s*:\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Singleline);
            return m.Success ? Regex.Unescape(m.Groups[1].Value) : null;
        }
        static double? ExtractNum(string src, string field)
        {
            var m = Regex.Match(src, $@"""{field}""\s*:\s*([0-9]+(?:\.[0-9]+)?)");
            return m.Success && double.TryParse(m.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
        }
        static bool ExtractBool(string src, string field)
        {
            var m = Regex.Match(src, $@"""{field}""\s*:\s*(true|false)", RegexOptions.IgnoreCase);
            return m.Success && bool.TryParse(m.Groups[1].Value, out var value) && value;
        }

        return new RescueMissionSuggestionResult
        {
            SuggestedMissionTitle = ExtractStr(text, "mission_title") ?? "Nhiệm vụ giải cứu",
            SuggestedMissionType = ExtractStr(text, "mission_type"),
            SuggestedPriorityScore = ExtractNum(text, "priority_score"),
            SuggestedSeverityLevel = ExtractStr(text, "severity_level"),
            OverallAssessment = ExtractStr(text, "overall_assessment")?.Replace("\n", " ").Replace("\r", " ").Trim(),
            EstimatedDuration = ExtractStr(text, "estimated_duration"),
            SpecialNotes = ExtractStr(text, "special_notes"),
            NeedsAdditionalDepot = ExtractBool(text, "needs_additional_depot"),
            ConfidenceScore = ExtractNum(text, "confidence_score") ?? 0.3
        };
    }

    private static void BackfillShortageItemIds(List<SupplyShortageDto> shortages, List<DepotSummary> depots)
    {
        if (shortages.Count == 0 || depots.Count == 0)
            return;

        var itemLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var depot in depots)
        {
            foreach (var inventory in depot.Inventories)
            {
                if (inventory.ItemId.HasValue && !string.IsNullOrWhiteSpace(inventory.ItemName))
                    itemLookup.TryAdd(NormalizeItemName(inventory.ItemName), inventory.ItemId.Value);
            }
        }

        foreach (var shortage in shortages)
        {
            if (shortage.ItemId.HasValue || string.IsNullOrWhiteSpace(shortage.ItemName))
                continue;

            var normalized = NormalizeItemName(shortage.ItemName);
            if (itemLookup.TryGetValue(normalized, out var exactId))
            {
                shortage.ItemId = exactId;
                continue;
            }

            foreach (var (key, id) in itemLookup)
            {
                if (normalized.Contains(key, StringComparison.OrdinalIgnoreCase)
                    || key.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    shortage.ItemId = id;
                    break;
                }
            }
        }
    }

    private static readonly Dictionary<string, string[]> GenericShortageAliasTokens =
        new(StringComparer.Ordinal)
        {
            ["thuoc men"] = ["thuoc", "y te", "medical", "so cuu", "cap cuu", "bo so cuu"],
            ["medicine"] = ["thuoc", "y te", "medical", "so cuu", "cap cuu", "bo so cuu"],
            ["medical"] = ["thuoc", "y te", "medical", "so cuu", "cap cuu", "bo so cuu"],
            ["y te"] = ["thuoc", "y te", "medical", "so cuu", "cap cuu", "bo so cuu"],
            ["so cuu"] = ["so cuu", "bo so cuu", "bang", "bong", "gac", "oxy", "thuoc"],
            ["first aid"] = ["so cuu", "bo so cuu", "bang", "bong", "gac", "oxy", "thuoc"],
            ["chan man"] = ["chan", "men", "giu nhiet", "suoi am", "suoi", "giu am"],
            ["blanket"] = ["chan", "men", "giu nhiet", "suoi am", "suoi", "giu am"],
            ["blankets"] = ["chan", "men", "giu nhiet", "suoi am", "suoi", "giu am"],
            ["giu am"] = ["chan", "men", "giu nhiet", "suoi am", "suoi", "giu am"],
            ["quan ao"] = ["ao", "quan", "chan", "men", "giu nhiet", "ao am"],
            ["clothing"] = ["ao", "quan", "chan", "men", "giu nhiet", "ao am"]
        };

    private static readonly HashSet<string> GenericShortageLabels =
    [
        "thuoc men",
        "medicine",
        "medical",
        "medical supplies",
        "y te",
        "so cuu",
        "first aid",
        "chan man",
        "blanket",
        "blankets",
        "giu am",
        "quan ao",
        "clothing"
    ];

    private static void ReconcileSupplyShortagesWithInventory(
        List<SupplyShortageDto> shortages,
        IReadOnlyCollection<DepotSummary> depots,
        IReadOnlyCollection<SuggestedActivityDto> activities)
    {
        if (shortages.Count == 0 || depots.Count == 0)
            return;

        var selectedDepot = GetSingleDepotSelection(activities);
        var depotLookup = depots.ToDictionary(depot => depot.Id);

        foreach (var shortage in shortages)
        {
            var depotId = shortage.SelectedDepotId ?? selectedDepot?.DepotId;
            if (!depotId.HasValue || !depotLookup.TryGetValue(depotId.Value, out var depot))
                continue;

            shortage.SelectedDepotId ??= depot.Id;
            shortage.SelectedDepotName ??= depot.Name;

            ReconcileSupplyShortageWithDepotInventory(shortage, depot);
        }
    }

    private static void ReconcileSupplyShortageWithDepotInventory(
        SupplyShortageDto shortage,
        DepotSummary depot)
    {
        if (string.IsNullOrWhiteSpace(shortage.ItemName) || depot.Inventories.Count == 0)
            return;

        var normalizedShortageName = NormalizeItemName(shortage.ItemName);
        var matchingItems = ResolveMatchingDepotInventoryItems(shortage, depot.Inventories, normalizedShortageName);
        if (matchingItems.Count == 0)
            return;

        var bestMatch = matchingItems[0];
        var isGenericShortage = IsGenericShortageLabel(normalizedShortageName);
        var totalAvailable = matchingItems.Sum(item => Math.Max(item.AvailableQuantity, 0));

        if (!shortage.ItemId.HasValue && bestMatch.ItemId.HasValue && (!isGenericShortage || matchingItems.Count == 1))
            shortage.ItemId = bestMatch.ItemId;

        if (!string.IsNullOrWhiteSpace(bestMatch.Unit)
            && (string.IsNullOrWhiteSpace(shortage.Unit)
                || (isGenericShortage && matchingItems.Count == 1)))
        {
            shortage.Unit = bestMatch.Unit;
        }

        if (isGenericShortage)
        {
            if (matchingItems.Count == 1 && !string.IsNullOrWhiteSpace(bestMatch.ItemName))
                shortage.ItemName = bestMatch.ItemName;

            if (totalAvailable > 0)
                shortage.AvailableQuantity = totalAvailable;
        }
        else
        {
            shortage.ItemName = bestMatch.ItemName;
            shortage.AvailableQuantity = Math.Max(bestMatch.AvailableQuantity, 0);
        }

        if (shortage.NeededQuantity <= 0 && shortage.MissingQuantity > 0)
            shortage.NeededQuantity = shortage.AvailableQuantity + shortage.MissingQuantity;

        if (shortage.NeededQuantity > 0)
            shortage.MissingQuantity = Math.Max(shortage.NeededQuantity - shortage.AvailableQuantity, 0);
    }

    private static List<DepotInventoryItemDto> ResolveMatchingDepotInventoryItems(
        SupplyShortageDto shortage,
        IReadOnlyCollection<DepotInventoryItemDto> inventories,
        string normalizedShortageName)
    {
        if (string.IsNullOrWhiteSpace(normalizedShortageName))
            return [];

        var searchTokens = ResolveGenericShortageSearchTokens(normalizedShortageName);

        return inventories
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemName))
            .Select(item => new
            {
                Item = item,
                Score = ScoreSupplyShortageInventoryMatch(shortage, item, normalizedShortageName, searchTokens)
            })
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenByDescending(entry => entry.Item.AvailableQuantity)
            .ThenBy(entry => entry.Item.ItemName, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Item)
            .ToList();
    }

    private static HashSet<string> ResolveGenericShortageSearchTokens(string normalizedShortageName)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal)
        {
            normalizedShortageName
        };

        foreach (var word in normalizedShortageName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (word.Length >= 3)
                tokens.Add(word);
        }

        foreach (var (alias, aliasTokens) in GenericShortageAliasTokens)
        {
            if (!normalizedShortageName.Contains(alias, StringComparison.Ordinal)
                && !alias.Contains(normalizedShortageName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var aliasToken in aliasTokens.Select(NormalizeItemName))
            {
                if (!string.IsNullOrWhiteSpace(aliasToken))
                    tokens.Add(aliasToken);
            }
        }

        return tokens;
    }

    private static int ScoreSupplyShortageInventoryMatch(
        SupplyShortageDto shortage,
        DepotInventoryItemDto inventory,
        string normalizedShortageName,
        IReadOnlyCollection<string> searchTokens)
    {
        var normalizedInventoryName = NormalizeItemName(inventory.ItemName);
        if (string.IsNullOrWhiteSpace(normalizedInventoryName))
            return 0;

        var score = 0;

        if (shortage.ItemId.HasValue && inventory.ItemId == shortage.ItemId)
            score += 1000;

        if (string.Equals(normalizedInventoryName, normalizedShortageName, StringComparison.Ordinal))
            score += 400;
        else if (normalizedInventoryName.Contains(normalizedShortageName, StringComparison.Ordinal)
                 || normalizedShortageName.Contains(normalizedInventoryName, StringComparison.Ordinal))
            score += 200;

        score += searchTokens
            .Where(token => normalizedInventoryName.Contains(token, StringComparison.Ordinal))
            .Sum(token => Math.Max(token.Length, 3) * 10);

        if (!string.IsNullOrWhiteSpace(shortage.Unit)
            && !string.IsNullOrWhiteSpace(inventory.Unit)
            && string.Equals(NormalizeItemName(shortage.Unit), NormalizeItemName(inventory.Unit), StringComparison.Ordinal))
        {
            score += 120;
        }

        return score;
    }

    private static bool IsGenericShortageLabel(string normalizedShortageName)
    {
        if (GenericShortageLabels.Contains(normalizedShortageName))
            return true;

        return GenericShortageAliasTokens.Keys.Any(alias =>
            normalizedShortageName.Contains(alias, StringComparison.Ordinal)
            || alias.Contains(normalizedShortageName, StringComparison.Ordinal));
    }

    private static void NormalizeSupplyShortages(RescueMissionSuggestionResult result)
    {
        var selectedDepot = GetSingleDepotSelection(result.SuggestedActivities);

        result.SupplyShortages = result.SupplyShortages
            .Select(shortage => NormalizeSupplyShortage(shortage, selectedDepot))
            .Where(shortage => (!string.IsNullOrWhiteSpace(shortage.ItemName) || shortage.ItemId.HasValue)
                && shortage.MissingQuantity > 0)
            .GroupBy(shortage => new
            {
                shortage.SosRequestId,
                ItemKey = shortage.ItemId?.ToString() ?? NormalizeItemName(shortage.ItemName),
                shortage.SelectedDepotId
            })
            .Select(group => group.First())
            .ToList();

        result.NeedsAdditionalDepot = result.SupplyShortages.Count > 0;

        if (!result.NeedsAdditionalDepot)
            return;

        result.SpecialNotes = AppendSpecialNote(result.SpecialNotes, BuildShortageCoordinatorNote(result.SupplyShortages));
    }

    private static SupplyShortageDto NormalizeSupplyShortage(
        SupplyShortageDto shortage,
        (int DepotId, string? DepotName)? selectedDepot)
    {
        var normalized = CloneSupplyShortage(shortage);

        normalized.AvailableQuantity = Math.Max(normalized.AvailableQuantity, 0);
        if (normalized.NeededQuantity <= 0 && normalized.MissingQuantity > 0)
            normalized.NeededQuantity = normalized.AvailableQuantity + normalized.MissingQuantity;

        if (normalized.MissingQuantity <= 0)
            normalized.MissingQuantity = Math.Max(normalized.NeededQuantity - normalized.AvailableQuantity, 0);

        if (normalized.SelectedDepotId is null && selectedDepot.HasValue)
            normalized.SelectedDepotId = selectedDepot.Value.DepotId;

        if (string.IsNullOrWhiteSpace(normalized.SelectedDepotName) && selectedDepot.HasValue)
            normalized.SelectedDepotName = selectedDepot.Value.DepotName;

        return normalized;
    }

    private static (int DepotId, string? DepotName)? GetSingleDepotSelection(IEnumerable<SuggestedActivityDto> activities)
    {
        var depots = activities
            .Where(activity => activity.DepotId.HasValue)
            .GroupBy(activity => activity.DepotId!.Value)
            .Select(group => new
            {
                DepotId = group.Key,
                DepotName = group.Select(activity => activity.DepotName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            })
            .ToList();

        return depots.Count == 1 ? (depots[0].DepotId, depots[0].DepotName) : null;
    }

    private static void ApplySingleDepotConstraint(RescueMissionSuggestionResult result)
    {
        var depots = result.SuggestedActivities
            .Where(activity => activity.DepotId.HasValue)
            .GroupBy(activity => activity.DepotId!.Value)
            .Select(group => group.First())
            .OrderBy(activity => activity.DepotId)
            .ToList();

        if (depots.Count <= 1)
            return;

        var depotLabel = string.Join(
            ", ",
            depots.Select(activity => string.IsNullOrWhiteSpace(activity.DepotName)
                ? $"#{activity.DepotId}"
                : $"{activity.DepotName} (#{activity.DepotId})"));

        result.NeedsManualReview = true;
        result.SpecialNotes = AppendSpecialNote(
            result.SpecialNotes,
            $"Plan hiện đang dùng nhiều kho: {depotLabel}. Backend yêu cầu AI chỉ chọn một kho phù hợp nhất cho toàn mission.");
    }

    private static void NormalizeEstimatedDurations(RescueMissionSuggestionResult result)
    {
        var totalMinutes = 0;
        var validActivityCount = 0;

        foreach (var activity in result.SuggestedActivities.OrderBy(activity => activity.Step))
        {
            if (string.IsNullOrWhiteSpace(activity.EstimatedTime))
            {
                result.NeedsManualReview = true;
                result.SpecialNotes = AppendSpecialNote(
                    result.SpecialNotes,
                    $"Activity step {activity.Step} ({activity.ActivityType}) chưa có estimated_time hợp lệ.");
                continue;
            }

            if (!TryParseDurationToMinutes(activity.EstimatedTime, out var activityMinutes))
            {
                result.NeedsManualReview = true;
                result.SpecialNotes = AppendSpecialNote(
                    result.SpecialNotes,
                    $"Activity step {activity.Step} ({activity.ActivityType}) có estimated_time khó hiểu: '{activity.EstimatedTime}'.");
                continue;
            }

            activity.EstimatedTime = FormatDuration(activityMinutes);
            totalMinutes += activityMinutes;
            validActivityCount++;
        }

        if (validActivityCount == result.SuggestedActivities.Count && validActivityCount > 0)
        {
            result.EstimatedDuration = FormatDuration(totalMinutes);
            return;
        }

        if (TryParseDurationToMinutes(result.EstimatedDuration, out var missionMinutes))
        {
            result.EstimatedDuration = FormatDuration(missionMinutes);
            return;
        }

        if (result.SuggestedActivities.Count > 0)
        {
            result.NeedsManualReview = true;
            result.SpecialNotes = AppendSpecialNote(
                result.SpecialNotes,
                "Mission chưa có estimated_duration hợp lệ để coordinator kiểm tra.");
        }
    }

    private static bool TryParseDurationToMinutes(string? rawText, out int totalMinutes)
    {
        totalMinutes = 0;

        if (string.IsNullOrWhiteSpace(rawText))
            return false;

        var text = rawText.Trim().ToLowerInvariant();

        if (int.TryParse(text, out var numericMinutes))
        {
            totalMinutes = numericMinutes;
            return numericMinutes > 0;
        }

        var hourMatch = Regex.Match(text, @"(?<value>\d+)\s*(giờ|gio|hour|hours|hr|hrs|h)");
        var minuteMatch = Regex.Match(text, @"(?<value>\d+)\s*(phút|phut|minute|minutes|min|mins|m)");

        if (!hourMatch.Success && !minuteMatch.Success)
            return false;

        if (hourMatch.Success)
            totalMinutes += int.Parse(hourMatch.Groups["value"].Value) * 60;

        if (minuteMatch.Success)
            totalMinutes += int.Parse(minuteMatch.Groups["value"].Value);

        return totalMinutes > 0;
    }

    private static string FormatDuration(int totalMinutes)
    {
        var safeMinutes = Math.Max(totalMinutes, 1);
        var hours = safeMinutes / 60;
        var minutes = safeMinutes % 60;

        if (hours <= 0)
            return $"{safeMinutes} phút";

        return minutes == 0
            ? $"{hours} giờ"
            : $"{hours} giờ {minutes} phút";
    }

    private static string BuildShortageCoordinatorNote(IReadOnlyCollection<SupplyShortageDto> shortages)
    {
        var details = shortages
            .Select(shortage =>
            {
                var sosPrefix = shortage.SosRequestId.HasValue ? $"[SOS ID {shortage.SosRequestId.Value}] " : string.Empty;
                var unitSuffix = string.IsNullOrWhiteSpace(shortage.Unit) ? string.Empty : $" {shortage.Unit}";
                return $"{sosPrefix}{shortage.ItemName} thiếu x{shortage.MissingQuantity}{unitSuffix}";
            })
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return details.Count == 0
            ? "Coordinator cần bổ sung thêm kho/nguồn cấp phát vì kho đã chọn không đủ vật phẩm."
            : "Coordinator cần bổ sung thêm kho/nguồn cấp phát. Thiếu: " + string.Join("; ", details) + ".";
    }

    private static bool IsCollectActivity(SuggestedActivityDto activity) =>
        string.Equals(activity.ActivityType, CollectSuppliesActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsReturnActivity(SuggestedActivityDto activity) =>
        string.Equals(activity.ActivityType, ReturnSuppliesActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsReturnAssemblyPointActivity(SuggestedActivityDto activity) =>
        string.Equals(activity.ActivityType, ReturnAssemblyPointActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsOnSiteActivity(SuggestedActivityDto activity) =>
        OnSiteActivityTypes.Contains(activity.ActivityType ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    private static bool HasRescueBranch(IEnumerable<SuggestedActivityDto> activities) =>
        activities.Any(activity =>
            string.Equals(activity.ActivityType, "RESCUE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(activity.ActivityType, "EVACUATE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(activity.ActivityType, "MEDICAL_AID", StringComparison.OrdinalIgnoreCase));

    private static bool HasReliefBranch(IEnumerable<SuggestedActivityDto> activities) =>
        activities.Any(activity =>
            string.Equals(activity.ActivityType, CollectSuppliesActivityType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase));

    private static bool HasMixedClusterRouteExpectation(IReadOnlyCollection<SosRequestSummary> sosRequests) =>
        sosRequests.Any(sos => SosRequestAiAnalysisHelper.IsRescueLikeRequestType(sos.SosType))
        && sosRequests.Any(sos => SosRequestAiAnalysisHelper.IsReliefRequestType(sos.SosType));

    private static MissionExecutionAssessment AssessExecutableMissionResult(
        RescueMissionSuggestionResult result,
        IReadOnlyCollection<SosRequestSummary> sosRequests,
        IReadOnlyCollection<SuggestedActivityDto>? expectedActivities = null)
    {
        if (sosRequests.Count == 0)
        {
            return new MissionExecutionAssessment(
                HasExecutableActivities: true,
                RequiresMixedBranches: false,
                HasRescueBranch: false,
                HasReliefBranch: false,
                FailureReason: null);
        }

        var executableActivities = result.SuggestedActivities
            .Where(activity => !IsReturnAssemblyPointActivity(activity))
            .ToList();

        var hasExecutableActivities = executableActivities.Count > 0;
        var hasRescueBranch = HasRescueBranch(executableActivities);
        var hasReliefBranch = HasReliefBranch(executableActivities);

        var requiresMixedBranches = expectedActivities is { Count: > 0 }
            ? HasRescueBranch(expectedActivities) && HasReliefBranch(expectedActivities)
            : HasMixedClusterRouteExpectation(sosRequests);

        string? failureReason = null;
        if (!hasExecutableActivities)
        {
            failureReason = "Mission suggestion must include executable activities for the current SOS cluster.";
        }
        else if (requiresMixedBranches && (!hasRescueBranch || !hasReliefBranch))
        {
            failureReason =
                "Mission suggestion for a mixed rescue-relief cluster must preserve both rescue and relief branches in executable activities.";
        }

        return new MissionExecutionAssessment(
            HasExecutableActivities: hasExecutableActivities,
            RequiresMixedBranches: requiresMixedBranches,
            HasRescueBranch: hasRescueBranch,
            HasReliefBranch: hasReliefBranch,
            FailureReason: failureReason);
    }

    private static MissionResultSelection SelectExecutableMissionResult(
        RescueMissionSuggestionResult candidate,
        IReadOnlyCollection<SosRequestSummary> sosRequests,
        MissionSuggestionPipelineState? pipelineState,
        IReadOnlyCollection<SuggestedActivityDto>? expectedActivities,
        string initialSource)
    {
        var assessment = AssessExecutableMissionResult(candidate, sosRequests, expectedActivities);
        if (assessment.IsExecutable)
        {
            return new MissionResultSelection(candidate, initialSource);
        }

        if (TryBuildDraftFallbackResult(candidate, sosRequests, pipelineState, expectedActivities, assessment, out var draftFallback))
            return new MissionResultSelection(draftFallback, "draft");

        var bestEffortFallback = BuildBestEffortFallbackResult(candidate, sosRequests, pipelineState, assessment);
        return new MissionResultSelection(bestEffortFallback, "salvaged");
    }

    private static HashSet<int> GetReferencedSosIds(SuggestedActivityDto activity)
    {
        var result = new HashSet<int>();
        if (activity.SosRequestId.HasValue)
            result.Add(activity.SosRequestId.Value);

        if (!string.IsNullOrWhiteSpace(activity.Description))
        {
            foreach (Match match in SosIdRegex.Matches(activity.Description))
            {
                if (int.TryParse(match.Groups[1].Value, out var sosId))
                    result.Add(sosId);
            }
        }

        return result;
    }

    private static int? GetPrimarySosId(SuggestedActivityDto activity)
    {
        if (activity.SosRequestId.HasValue)
            return activity.SosRequestId.Value;

        return GetReferencedSosIds(activity).OrderBy(x => x).FirstOrDefault();
    }

    private static (double Latitude, double Longitude)? ResolveActivityCoordinates(
        SuggestedActivityDto activity,
        IReadOnlyDictionary<int, SosRequestSummary> sosLookup)
    {
        if (activity.SosRequestId.HasValue
            && sosLookup.TryGetValue(activity.SosRequestId.Value, out var sos)
            && sos.Latitude.HasValue
            && sos.Longitude.HasValue)
        {
            return (sos.Latitude.Value, sos.Longitude.Value);
        }

        if (string.IsNullOrWhiteSpace(activity.Description))
            return null;

        var match = CoordinateRegex.Match(activity.Description);
        if (!match.Success)
            return null;

        if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lon))
        {
            return (lat, lon);
        }

        return null;
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        var deltaLat = (lat2 - lat1) * Math.PI / 180.0;
        var deltaLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
              + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
              * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static int GetRoutePriority(SuggestedActivityDto activity) =>
        (activity.ActivityType ?? string.Empty).ToUpperInvariant() switch
        {
            CollectSuppliesActivityType => 0,
            "DELIVER_SUPPLIES" => 1,
            "RESCUE" => 2,
            "MEDICAL_AID" => 3,
            "EVACUATE" => 4,
            ReturnSuppliesActivityType => 5,
            ReturnAssemblyPointActivityType => 6,
            _ => 99
        };

    private static int GetSequenceGroupPriority(
        SuggestedActivityDto activity,
        int? primarySosId)
    {
        if (activity.SuggestedTeam?.TeamId is > 0)
            return 0;

        if (!string.IsNullOrWhiteSpace(activity.CoordinationGroupKey))
            return 1;

        if (primarySosId.HasValue)
            return 2;

        return 3;
    }

    private static string BuildSequenceGroupKey(
        SuggestedActivityDto activity,
        int originalIndex,
        int? primarySosId)
    {
        if (activity.SuggestedTeam?.TeamId is > 0)
            return $"team:{activity.SuggestedTeam.TeamId}";

        if (!string.IsNullOrWhiteSpace(activity.CoordinationGroupKey))
            return $"coord:{activity.CoordinationGroupKey.Trim()}";

        if (primarySosId.HasValue)
            return $"sos:{primarySosId.Value}";

        return $"step:{originalIndex}";
    }

    private static SuggestedActivityDto CloneActivity(SuggestedActivityDto activity)
    {
        return new SuggestedActivityDto
        {
            Step = activity.Step,
            ActivityType = activity.ActivityType,
            Description = activity.Description,
            TargetVictimSummary = activity.TargetVictimSummary,
            TargetVictims = MissionActivityVictimContextHelper.CloneVictims(activity.TargetVictims),
            Priority = activity.Priority,
            EstimatedTime = activity.EstimatedTime,
            ExecutionMode = activity.ExecutionMode,
            RequiredTeamCount = activity.RequiredTeamCount,
            CoordinationGroupKey = activity.CoordinationGroupKey,
            CoordinationNotes = activity.CoordinationNotes,
            SosRequestId = activity.SosRequestId,
            DepotId = activity.DepotId,
            DepotName = activity.DepotName,
            DepotAddress = activity.DepotAddress,
            AssemblyPointId = activity.AssemblyPointId,
            AssemblyPointName = activity.AssemblyPointName,
            AssemblyPointLatitude = activity.AssemblyPointLatitude,
            AssemblyPointLongitude = activity.AssemblyPointLongitude,
            SuppliesToCollect = activity.SuppliesToCollect?.Select(s => new SupplyToCollectDto
            {
                ItemId = s.ItemId,
                ItemName = s.ItemName,
                Quantity = s.Quantity,
                Unit = s.Unit
            }).ToList(),
            SuggestedTeam = activity.SuggestedTeam == null ? null : new SuggestedTeamDto
            {
                TeamId = activity.SuggestedTeam.TeamId,
                TeamName = activity.SuggestedTeam.TeamName,
                TeamType = activity.SuggestedTeam.TeamType,
                Reason = activity.SuggestedTeam.Reason,
                AssemblyPointId = activity.SuggestedTeam.AssemblyPointId,
                AssemblyPointName = activity.SuggestedTeam.AssemblyPointName,
                Latitude = activity.SuggestedTeam.Latitude,
                Longitude = activity.SuggestedTeam.Longitude,
                DistanceKm = activity.SuggestedTeam.DistanceKm
            }
        };
    }

    private static bool TryBuildDraftFallbackResult(
        RescueMissionSuggestionResult seedResult,
        IReadOnlyCollection<SosRequestSummary> sosRequests,
        MissionSuggestionPipelineState? pipelineState,
        IReadOnlyCollection<SuggestedActivityDto>? expectedActivities,
        MissionExecutionAssessment assessment,
        out RescueMissionSuggestionResult result)
    {
        if (pipelineState?.DraftBody is not null)
        {
            result = MapDraftBodyToResult(
                pipelineState.DraftBody,
                seedResult.RawAiResponse ?? SerializeMissionDraftBody(pipelineState.DraftBody));
            result = MergeFallbackResult(seedResult, sosRequests, pipelineState, result);
            result.SpecialNotes = AppendSpecialNote(
                result.SpecialNotes,
                BuildExecutionFallbackNote(assessment, "draft"));
            result.NeedsManualReview = true;
            return true;
        }

        var fallbackActivities = pipelineState?.DraftActivities ?? expectedActivities?.Select(CloneActivity).ToList();
        if (fallbackActivities is not { Count: > 0 })
        {
            result = null!;
            return false;
        }

        result = BuildFallbackResultShell(seedResult, sosRequests, pipelineState);
        result.SuggestedActivities = fallbackActivities.Select(CloneActivity).ToList();
        result.SpecialNotes = AppendSpecialNote(
            result.SpecialNotes,
            BuildExecutionFallbackNote(assessment, "draft"));
        result.NeedsManualReview = true;
        return true;
    }

    private static RescueMissionSuggestionResult BuildBestEffortFallbackResult(
        RescueMissionSuggestionResult seedResult,
        IReadOnlyCollection<SosRequestSummary> sosRequests,
        MissionSuggestionPipelineState? pipelineState,
        MissionExecutionAssessment assessment)
    {
        var result = BuildFallbackResultShell(seedResult, sosRequests, pipelineState);
        result.SuggestedActivities = BuildBestEffortActivities(sosRequests, pipelineState);
        result.SpecialNotes = AppendSpecialNote(
            result.SpecialNotes,
            BuildExecutionFallbackNote(
                assessment,
                pipelineState is null ? "raw_sos" : "pipeline_fragments"));
        result.NeedsManualReview = true;
        return result;
    }

    private static RescueMissionSuggestionResult BuildFallbackResultShell(
        RescueMissionSuggestionResult seedResult,
        IReadOnlyCollection<SosRequestSummary> sosRequests,
        MissionSuggestionPipelineState? pipelineState)
    {
        var requirements = pipelineState?.Requirements;
        var depot = pipelineState?.Depot;
        var team = pipelineState?.Team;
        var result = new RescueMissionSuggestionResult
        {
            SuggestionId = seedResult.SuggestionId,
            IsSuccess = true,
            ErrorMessage = null,
            ModelName = seedResult.ModelName,
            ResponseTimeMs = seedResult.ResponseTimeMs,
            SuggestedMissionTitle = FirstNonEmpty(seedResult.SuggestedMissionTitle, requirements?.SuggestedMissionTitle),
            SuggestedMissionType = FirstNonEmpty(seedResult.SuggestedMissionType, requirements?.SuggestedMissionType),
            SuggestedPriorityScore = seedResult.SuggestedPriorityScore ?? requirements?.SuggestedPriorityScore,
            SuggestedSeverityLevel = FirstNonEmpty(seedResult.SuggestedSeverityLevel, requirements?.SuggestedSeverityLevel),
            OverallAssessment = FirstNonEmpty(seedResult.OverallAssessment, requirements?.OverallAssessment),
            SuggestedActivities = [],
            SuggestedResources = ChooseSuggestedResources(requirements?.SuggestedResources, seedResult.SuggestedResources),
            EstimatedDuration = FirstNonEmpty(seedResult.EstimatedDuration, requirements?.EstimatedDuration),
            SpecialNotes = JoinNotes(seedResult.SpecialNotes, requirements?.SpecialNotes, depot?.SpecialNotes, team?.SpecialNotes),
            MixedRescueReliefWarning = seedResult.MixedRescueReliefWarning,
            NeedsAdditionalDepot = seedResult.NeedsAdditionalDepot || requirements?.NeedsAdditionalDepot == true || depot?.NeedsAdditionalDepot == true,
            SupplyShortages = ChooseSupplyShortages(depot?.SupplyShortages, requirements?.SupplyShortages, seedResult.SupplyShortages),
            ConfidenceScore = seedResult.ConfidenceScore > 0
                ? seedResult.ConfidenceScore
                : CalculateDraftConfidence(
                    requirements?.ConfidenceScore ?? 0,
                    depot?.ConfidenceScore ?? 0,
                    team?.ConfidenceScore ?? 0),
            RawAiResponse = seedResult.RawAiResponse,
            NeedsManualReview = true,
            LowConfidenceWarning = seedResult.LowConfidenceWarning,
            MultiDepotRecommended = false,
            SuggestedTeam = CloneSuggestedTeam(seedResult.SuggestedTeam ?? team?.SuggestedTeam),
            PipelineMetadata = seedResult.PipelineMetadata
        };

        result.SuggestedMissionType ??= InferMissionType(sosRequests);

        var highestPriority = ResolveHighestPriority(requirements?.SosRequirements, sosRequests);
        result.SuggestedSeverityLevel ??= MapSeverityFromPriorityLabel(highestPriority);
        result.SuggestedPriorityScore ??= MapPriorityScore(highestPriority);
        result.SuggestedMissionTitle ??= BuildFallbackMissionTitle(result.SuggestedMissionType, sosRequests);
        result.OverallAssessment ??= BuildFallbackOverallAssessment(result.SuggestedMissionType, sosRequests);

        return result;
    }

    private static RescueMissionSuggestionResult MergeFallbackResult(
        RescueMissionSuggestionResult seedResult,
        IReadOnlyCollection<SosRequestSummary> sosRequests,
        MissionSuggestionPipelineState? pipelineState,
        RescueMissionSuggestionResult fallbackResult)
    {
        var merged = BuildFallbackResultShell(seedResult, sosRequests, pipelineState);
        merged.SuggestedMissionTitle = FirstNonEmpty(fallbackResult.SuggestedMissionTitle, merged.SuggestedMissionTitle);
        merged.SuggestedMissionType = FirstNonEmpty(fallbackResult.SuggestedMissionType, merged.SuggestedMissionType);
        merged.SuggestedPriorityScore = fallbackResult.SuggestedPriorityScore ?? merged.SuggestedPriorityScore;
        merged.SuggestedSeverityLevel = FirstNonEmpty(fallbackResult.SuggestedSeverityLevel, merged.SuggestedSeverityLevel);
        merged.OverallAssessment = FirstNonEmpty(fallbackResult.OverallAssessment, merged.OverallAssessment);
        merged.SuggestedActivities = fallbackResult.SuggestedActivities.Select(CloneActivity).ToList();
        merged.SuggestedResources = fallbackResult.SuggestedResources.Count > 0
            ? fallbackResult.SuggestedResources.Select(CloneSuggestedResource).ToList()
            : merged.SuggestedResources;
        merged.EstimatedDuration = FirstNonEmpty(fallbackResult.EstimatedDuration, merged.EstimatedDuration);
        merged.SpecialNotes = JoinNotes(merged.SpecialNotes, fallbackResult.SpecialNotes);
        merged.NeedsAdditionalDepot = fallbackResult.NeedsAdditionalDepot || merged.NeedsAdditionalDepot;
        merged.SupplyShortages = fallbackResult.SupplyShortages.Count > 0
            ? fallbackResult.SupplyShortages.Select(CloneSupplyShortage).ToList()
            : merged.SupplyShortages;
        merged.ConfidenceScore = fallbackResult.ConfidenceScore > 0 ? fallbackResult.ConfidenceScore : merged.ConfidenceScore;
        merged.SuggestedTeam = CloneSuggestedTeam(fallbackResult.SuggestedTeam ?? merged.SuggestedTeam);
        merged.MixedRescueReliefWarning = string.IsNullOrWhiteSpace(fallbackResult.MixedRescueReliefWarning)
            ? merged.MixedRescueReliefWarning
            : fallbackResult.MixedRescueReliefWarning;
        return merged;
    }

    private static List<SuggestedActivityDto> BuildBestEffortActivities(
        IReadOnlyCollection<SosRequestSummary> sosRequests,
        MissionSuggestionPipelineState? pipelineState)
    {
        if (pipelineState?.Requirements is not null
            && pipelineState.Depot is not null
            && pipelineState.Team is not null)
        {
            var draftBody = AssembleDraftBody(pipelineState.Requirements, pipelineState.Depot, pipelineState.Team);
            if (draftBody.Activities.Count > 0)
            {
                return draftBody.Activities
                    .Select(MapDraftActivityToSuggestedActivity)
                    .ToList();
            }
        }

        var activities = new List<SuggestedActivityDto>();
        if (pipelineState is not null)
            activities.AddRange(BuildActivitiesFromPipelineFragments(pipelineState));

        var activityLookup = activities
            .Select(activity => new
            {
                Activity = activity,
                PrimarySosId = GetPrimarySosId(activity)
            })
            .ToList();

        var sosLookup = sosRequests.ToDictionary(sos => sos.Id);
        var requirementLookup = pipelineState?.Requirements?.SosRequirements
            .ToDictionary(requirement => requirement.SosRequestId)
            ?? new Dictionary<int, MissionSosRequirementFragment>();

        var isMixedCluster = sosRequests.Any(sos => SosRequestAiAnalysisHelper.IsRescueLikeRequestType(sos.SosType))
            && sosRequests.Any(sos => SosRequestAiAnalysisHelper.IsReliefRequestType(sos.SosType));
        var hasUrgentRescue = sosRequests.Any(sos =>
            SosRequestAiAnalysisHelper.IsRescueLikeRequestType(sos.SosType)
            && SosRequestAiAnalysisHelper.HasUrgentMixedMissionConstraint(sos.AiAnalysis, sos.PriorityLevel));
        var waitableMixed = isMixedCluster && !hasUrgentRescue;
        var coordinationPrefix = waitableMixed ? "00-mixed-route" : "10-relief-route";
        var rescueCoordinationPrefix = hasUrgentRescue ? "00-urgent-rescue-route" : coordinationPrefix;

        foreach (var sos in sosRequests.OrderBy(s => s.CreatedAt ?? DateTime.MinValue).ThenBy(s => s.Id))
        {
            requirementLookup.TryGetValue(sos.Id, out var requirement);
            var isRescueLike = IsRescueLikeFallbackRequest(sos, requirement);
            var isReliefLike = IsReliefLikeFallbackRequest(sos, requirement);

            if (!isRescueLike && !isReliefLike)
                isRescueLike = true;

            var existingActivities = activityLookup
                .Where(item => item.PrimarySosId == sos.Id)
                .Select(item => item.Activity)
                .ToList();

            if (isReliefLike && !HasReliefBranch(existingActivities))
            {
                var reliefActivities = BuildFallbackReliefActivities(
                    sos,
                    requirement,
                    pipelineState,
                    waitableMixed ? coordinationPrefix : "10-relief-route");
                activities.AddRange(reliefActivities);
                activityLookup.AddRange(reliefActivities.Select(activity => new { Activity = activity, PrimarySosId = (int?)sos.Id }));
            }

            if (isRescueLike && !HasRescueBranch(existingActivities))
            {
                var rescueActivities = BuildFallbackRescueActivities(
                    sos,
                    requirement,
                    pipelineState,
                    hasUrgentRescue ? rescueCoordinationPrefix : coordinationPrefix,
                    assignGlobalTeam: !hasUrgentRescue || pipelineState?.Team?.SuggestedTeam is not null);
                activities.AddRange(rescueActivities);
                activityLookup.AddRange(rescueActivities.Select(activity => new { Activity = activity, PrimarySosId = (int?)sos.Id }));
            }
        }

        if (activities.Count == 0 && sosRequests.Count > 0)
        {
            activities.AddRange(BuildFallbackRescueActivities(
                sosRequests.OrderBy(s => s.CreatedAt ?? DateTime.MinValue).ThenBy(s => s.Id).First(),
                requirement: null,
                pipelineState,
                coordinationGroupKey: "00-minimal-route",
                assignGlobalTeam: false));
        }

        for (var index = 0; index < activities.Count; index++)
            activities[index].Step = index + 1;

        return activities;
    }

    private static IEnumerable<SuggestedActivityDto> BuildActivitiesFromPipelineFragments(MissionSuggestionPipelineState state)
    {
        var activities = new List<SuggestedActivityDto>();
        var assignmentLookup = state.Team?.ActivityAssignments
            .Where(assignment => !string.IsNullOrWhiteSpace(assignment.ActivityKey))
            .ToDictionary(assignment => assignment.ActivityKey, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, MissionActivityAssignmentFragment>(StringComparer.OrdinalIgnoreCase);

        if (state.Depot is not null)
        {
            foreach (var activity in state.Depot.Activities.OrderBy(item => item.Step))
            {
                var draft = MapActivityFragmentToDraft(activity);
                if (assignmentLookup.TryGetValue(activity.ActivityKey, out var assignment))
                {
                    draft.ExecutionMode = assignment.ExecutionMode ?? draft.ExecutionMode;
                    draft.RequiredTeamCount = assignment.RequiredTeamCount ?? draft.RequiredTeamCount;
                    draft.CoordinationGroupKey = assignment.CoordinationGroupKey ?? draft.CoordinationGroupKey;
                    draft.CoordinationNotes = assignment.CoordinationNotes ?? draft.CoordinationNotes;
                    draft.SuggestedTeam = CloneSuggestedTeam(assignment.SuggestedTeam) ?? draft.SuggestedTeam;
                }

                activities.Add(MapDraftActivityToSuggestedActivity(draft));
            }
        }

        if (state.Team is not null)
        {
            activities.AddRange(state.Team.AdditionalActivities
                .OrderBy(item => item.Step)
                .Select(MapActivityFragmentToDraft)
                .Select(MapDraftActivityToSuggestedActivity));
        }

        return activities;
    }

    private static List<SuggestedActivityDto> BuildFallbackReliefActivities(
        SosRequestSummary sos,
        MissionSosRequirementFragment? requirement,
        MissionSuggestionPipelineState? pipelineState,
        string coordinationGroupKey)
    {
        var depotSelection = ResolveFallbackDepotSelection(pipelineState);
        var supplies = ResolveFallbackSupplies(requirement);
        var suggestedTeam = ResolveFallbackSuggestedTeam(pipelineState, assignGlobalTeam: true);
        var priority = ResolveFallbackPriorityLabel(requirement, sos);

        return
        [
            new SuggestedActivityDto
            {
                ActivityType = CollectSuppliesActivityType,
                Description = depotSelection.DepotId.HasValue
                    ? $"Lấy vật phẩm tiếp tế tại {depotSelection.DepotName ?? $"kho #{depotSelection.DepotId.Value}"} cho SOS #{sos.Id}."
                    : $"Chuẩn bị vật phẩm tiếp tế thiết yếu cho SOS #{sos.Id}.",
                Priority = priority,
                EstimatedTime = "20 phút",
                ExecutionMode = suggestedTeam is null ? null : SingleTeamExecutionMode,
                RequiredTeamCount = suggestedTeam is null ? null : 1,
                CoordinationGroupKey = coordinationGroupKey,
                SosRequestId = sos.Id,
                DepotId = depotSelection.DepotId,
                DepotName = depotSelection.DepotName,
                DepotAddress = depotSelection.DepotAddress,
                DestinationName = depotSelection.DepotName,
                DestinationLatitude = depotSelection.DepotLatitude,
                DestinationLongitude = depotSelection.DepotLongitude,
                SuppliesToCollect = supplies.Select(CloneSupply).ToList(),
                SuggestedTeam = CloneSuggestedTeam(suggestedTeam)
            },
            new SuggestedActivityDto
            {
                ActivityType = "DELIVER_SUPPLIES",
                Description = $"Giao vật phẩm tiếp tế cho SOS #{sos.Id}.",
                Priority = priority,
                EstimatedTime = "20 phút",
                ExecutionMode = suggestedTeam is null ? null : SingleTeamExecutionMode,
                RequiredTeamCount = suggestedTeam is null ? null : 1,
                CoordinationGroupKey = coordinationGroupKey,
                SosRequestId = sos.Id,
                DepotId = depotSelection.DepotId,
                DepotName = depotSelection.DepotName,
                DepotAddress = depotSelection.DepotAddress,
                DestinationName = depotSelection.DepotName,
                DestinationLatitude = depotSelection.DepotLatitude,
                DestinationLongitude = depotSelection.DepotLongitude,
                SuppliesToCollect = supplies.Select(CloneSupply).ToList(),
                SuggestedTeam = CloneSuggestedTeam(suggestedTeam)
            }
        ];
    }

    private static List<SuggestedActivityDto> BuildFallbackRescueActivities(
        SosRequestSummary sos,
        MissionSosRequirementFragment? requirement,
        MissionSuggestionPipelineState? pipelineState,
        string coordinationGroupKey,
        bool assignGlobalTeam)
    {
        var activities = new List<SuggestedActivityDto>();
        var priority = ResolveFallbackPriorityLabel(requirement, sos);
        var suggestedTeam = ResolveFallbackSuggestedTeam(pipelineState, assignGlobalTeam);
        var assemblyPoint = ResolveFallbackAssemblyPoint(suggestedTeam, pipelineState);

        activities.Add(new SuggestedActivityDto
        {
            ActivityType = "RESCUE",
            Description = $"Tiếp cận và cứu hộ nạn nhân của SOS #{sos.Id}.",
            Priority = priority,
            EstimatedTime = "30 phút",
            ExecutionMode = suggestedTeam is null ? null : SingleTeamExecutionMode,
            RequiredTeamCount = suggestedTeam is null ? null : 1,
            CoordinationGroupKey = coordinationGroupKey,
            SosRequestId = sos.Id,
            AssemblyPointId = assemblyPoint.AssemblyPointId,
            AssemblyPointName = assemblyPoint.AssemblyPointName,
            AssemblyPointLatitude = assemblyPoint.AssemblyPointLatitude,
            AssemblyPointLongitude = assemblyPoint.AssemblyPointLongitude,
            DestinationName = assemblyPoint.AssemblyPointName,
            DestinationLatitude = assemblyPoint.AssemblyPointLatitude,
            DestinationLongitude = assemblyPoint.AssemblyPointLongitude,
            SuggestedTeam = CloneSuggestedTeam(suggestedTeam)
        });

        if (ShouldAddFallbackMedicalAid(requirement, sos))
        {
            activities.Add(new SuggestedActivityDto
            {
                ActivityType = "MEDICAL_AID",
                Description = $"Sơ cứu và ổn định y tế cho nạn nhân của SOS #{sos.Id}.",
                Priority = priority,
                EstimatedTime = "20 phút",
                ExecutionMode = suggestedTeam is null ? null : SingleTeamExecutionMode,
                RequiredTeamCount = suggestedTeam is null ? null : 1,
                CoordinationGroupKey = coordinationGroupKey,
                SosRequestId = sos.Id,
                AssemblyPointId = assemblyPoint.AssemblyPointId,
                AssemblyPointName = assemblyPoint.AssemblyPointName,
                AssemblyPointLatitude = assemblyPoint.AssemblyPointLatitude,
                AssemblyPointLongitude = assemblyPoint.AssemblyPointLongitude,
                DestinationName = assemblyPoint.AssemblyPointName,
                DestinationLatitude = assemblyPoint.AssemblyPointLatitude,
                DestinationLongitude = assemblyPoint.AssemblyPointLongitude,
                SuggestedTeam = CloneSuggestedTeam(suggestedTeam)
            });
        }

        if (ShouldAddFallbackEvacuation(requirement, sos))
        {
            activities.Add(new SuggestedActivityDto
            {
                ActivityType = "EVACUATE",
                Description = $"Đưa nạn nhân của SOS #{sos.Id} đến nơi an toàn.",
                Priority = priority,
                EstimatedTime = "25 phút",
                ExecutionMode = suggestedTeam is null ? null : SingleTeamExecutionMode,
                RequiredTeamCount = suggestedTeam is null ? null : 1,
                CoordinationGroupKey = coordinationGroupKey,
                SosRequestId = sos.Id,
                AssemblyPointId = assemblyPoint.AssemblyPointId,
                AssemblyPointName = assemblyPoint.AssemblyPointName,
                AssemblyPointLatitude = assemblyPoint.AssemblyPointLatitude,
                AssemblyPointLongitude = assemblyPoint.AssemblyPointLongitude,
                DestinationName = assemblyPoint.AssemblyPointName,
                DestinationLatitude = assemblyPoint.AssemblyPointLatitude,
                DestinationLongitude = assemblyPoint.AssemblyPointLongitude,
                SuggestedTeam = CloneSuggestedTeam(suggestedTeam)
            });
        }

        return activities;
    }

    private static (int? DepotId, string? DepotName, string? DepotAddress, double? DepotLatitude, double? DepotLongitude) ResolveFallbackDepotSelection(
        MissionSuggestionPipelineState? pipelineState)
    {
        var depotActivity = pipelineState?.Depot?.Activities
            .FirstOrDefault(activity => activity.DepotId.HasValue || !string.IsNullOrWhiteSpace(activity.DepotName));
        if (depotActivity is not null)
        {
            return (
                depotActivity.DepotId,
                depotActivity.DepotName,
                depotActivity.DepotAddress,
                depotActivity.DepotLatitude,
                depotActivity.DepotLongitude);
        }

        var shortage = pipelineState?.Depot?.SupplyShortages
            .FirstOrDefault(item => item.SelectedDepotId.HasValue || !string.IsNullOrWhiteSpace(item.SelectedDepotName));
        if (shortage is not null)
        {
            return (
                shortage.SelectedDepotId,
                shortage.SelectedDepotName,
                null,
                null,
                null);
        }

        return (null, null, null, null, null);
    }

    private static (int? AssemblyPointId, string? AssemblyPointName, double? AssemblyPointLatitude, double? AssemblyPointLongitude) ResolveFallbackAssemblyPoint(
        SuggestedTeamDto? suggestedTeam,
        MissionSuggestionPipelineState? pipelineState)
    {
        var rescueActivity = pipelineState?.Team?.AdditionalActivities
            .FirstOrDefault(activity => activity.AssemblyPointId.HasValue || !string.IsNullOrWhiteSpace(activity.AssemblyPointName));
        if (rescueActivity is not null)
        {
            return (
                rescueActivity.AssemblyPointId,
                rescueActivity.AssemblyPointName,
                rescueActivity.AssemblyPointLatitude,
                rescueActivity.AssemblyPointLongitude);
        }

        return (
            suggestedTeam?.AssemblyPointId,
            suggestedTeam?.AssemblyPointName,
            suggestedTeam?.Latitude,
            suggestedTeam?.Longitude);
    }

    private static SuggestedTeamDto? ResolveFallbackSuggestedTeam(
        MissionSuggestionPipelineState? pipelineState,
        bool assignGlobalTeam)
    {
        if (!assignGlobalTeam)
            return null;

        return CloneSuggestedTeam(pipelineState?.Team?.SuggestedTeam);
    }

    private static List<SupplyToCollectDto> ResolveFallbackSupplies(MissionSosRequirementFragment? requirement)
    {
        if (requirement?.RequiredSupplies is { Count: > 0 })
        {
            return requirement.RequiredSupplies
                .Select(supply => new SupplyToCollectDto
                {
                    ItemName = string.IsNullOrWhiteSpace(supply.ItemName) ? "Nhu yếu phẩm thiết yếu" : supply.ItemName,
                    Quantity = Math.Max(supply.Quantity, 1),
                    Unit = supply.Unit
                })
                .ToList();
        }

        return
        [
            new SupplyToCollectDto
            {
                ItemName = "Nhu yếu phẩm thiết yếu",
                Quantity = 1,
                Unit = "gói"
            }
        ];
    }

    private static bool IsRescueLikeFallbackRequest(
        SosRequestSummary sos,
        MissionSosRequirementFragment? requirement)
    {
        if (SosRequestAiAnalysisHelper.IsRescueLikeRequestType(sos.SosType))
            return true;

        if (requirement?.NeedsImmediateSafeTransfer == true || requirement?.CanWaitForCombinedMission == false)
            return true;

        if (requirement?.RequiredTeams.Count > 0)
            return true;

        return ContainsFallbackKeyword(
            $"{requirement?.Summary} {requirement?.HandlingReason} {sos.RawMessage} {sos.StructuredData}",
            "CUU", "RESCUE", "MAC KET", "INJUR", "BAT TINH", "SO CUU", "EVACUATE", "AN TOAN", "MEDICAL");
    }

    private static bool IsReliefLikeFallbackRequest(
        SosRequestSummary sos,
        MissionSosRequirementFragment? requirement)
    {
        if (SosRequestAiAnalysisHelper.IsReliefRequestType(sos.SosType))
            return true;

        if (requirement?.RequiredSupplies.Count > 0)
            return true;

        return ContainsFallbackKeyword(
            $"{requirement?.Summary} {requirement?.HandlingReason} {sos.RawMessage} {sos.StructuredData}",
            "TIEP TE", "SUPPLY", "LUONG THUC", "THUC PHAM", "NUOC", "BLANKET", "QUAN AO", "THUOC");
    }

    private static bool ShouldAddFallbackMedicalAid(
        MissionSosRequirementFragment? requirement,
        SosRequestSummary sos)
    {
        if (ContainsFallbackKeyword(
                $"{requirement?.Summary} {requirement?.HandlingReason} {sos.RawMessage} {sos.StructuredData}",
                "Y TE", "MEDICAL", "SO CUU", "GAY XUONG", "MAT NHIET", "CHAY MAU", "BAT TINH"))
        {
            return true;
        }

        var priority = ResolveFallbackPriorityLabel(requirement, sos);
        return string.Equals(priority, "Critical", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldAddFallbackEvacuation(
        MissionSosRequirementFragment? requirement,
        SosRequestSummary sos)
    {
        if (requirement?.NeedsImmediateSafeTransfer == true || requirement?.CanWaitForCombinedMission == false)
            return true;

        return ContainsFallbackKeyword(
            $"{requirement?.Summary} {requirement?.HandlingReason} {sos.RawMessage} {sos.StructuredData}",
            "AN TOAN", "SAFE", "EVACUATE", "DI CHUYEN");
    }

    private static string ResolveFallbackPriorityLabel(
        MissionSosRequirementFragment? requirement,
        SosRequestSummary sos)
    {
        return requirement?.Priority
            ?? SosRequestAiAnalysisHelper.ResolveSuggestedPriority(sos.AiAnalysis, sos.PriorityLevel)
            ?? "High";
    }

    private static string ResolveHighestPriority(
        IReadOnlyCollection<MissionSosRequirementFragment>? requirements,
        IReadOnlyCollection<SosRequestSummary> sosRequests)
    {
        string? highest = null;

        if (requirements is { Count: > 0 })
        {
            foreach (var priority in requirements.Select(requirement => requirement.Priority))
                highest = SelectHigherPriority(highest, priority);
        }

        foreach (var sos in sosRequests)
            highest = SelectHigherPriority(highest, SosRequestAiAnalysisHelper.ResolveSuggestedPriority(sos.AiAnalysis, sos.PriorityLevel));

        return highest ?? "High";
    }

    private static string InferMissionType(IReadOnlyCollection<SosRequestSummary> sosRequests)
    {
        var hasRescue = sosRequests.Any(sos => SosRequestAiAnalysisHelper.IsRescueLikeRequestType(sos.SosType));
        var hasRelief = sosRequests.Any(sos => SosRequestAiAnalysisHelper.IsReliefRequestType(sos.SosType));

        if (hasRescue && hasRelief)
            return "MIXED";

        if (hasRescue)
            return "RESCUE";

        if (hasRelief)
            return "SUPPLY";

        return "RESCUE";
    }

    private static string? BuildFallbackMissionTitle(
        string? missionType,
        IReadOnlyCollection<SosRequestSummary> sosRequests)
    {
        var label = missionType?.Trim().ToUpperInvariant() switch
        {
            "MIXED" => "Cứu hộ và cứu trợ",
            "SUPPLY" => "Cứu trợ",
            _ => "Cứu hộ"
        };
        var sosIds = string.Join(", ", sosRequests.Select(sos => $"#{sos.Id}"));
        return string.IsNullOrWhiteSpace(sosIds)
            ? label
            : $"{label} cho cluster SOS {sosIds}";
    }

    private static string? BuildFallbackOverallAssessment(
        string? missionType,
        IReadOnlyCollection<SosRequestSummary> sosRequests)
    {
        var orderedIds = sosRequests
            .OrderBy(sos => sos.CreatedAt ?? DateTime.MinValue)
            .ThenBy(sos => sos.Id)
            .Select(sos => $"[SOS ID {sos.Id}]")
            .ToList();

        if (orderedIds.Count == 0)
            return null;

        return missionType?.Trim().ToUpperInvariant() switch
        {
            "MIXED" => $"Ưu tiên lập route mixed an toàn cho {string.Join(", ", orderedIds)} và giữ cảnh báo tách cluster để coordinator rà soát.",
            "SUPPLY" => $"Lập route tiếp tế cho {string.Join(", ", orderedIds)} để đội có thể triển khai ngay.",
            _ => $"Lập route cứu hộ cho {string.Join(", ", orderedIds)} để đội có thể triển khai ngay."
        };
    }

    private static string MapSeverityFromPriorityLabel(string? priority) =>
        (priority ?? string.Empty).Trim() switch
        {
            var value when value.Equals("Critical", StringComparison.OrdinalIgnoreCase) => "Critical",
            var value when value.Equals("High", StringComparison.OrdinalIgnoreCase) => "Severe",
            var value when value.Equals("Medium", StringComparison.OrdinalIgnoreCase) => "Moderate",
            var value when value.Equals("Low", StringComparison.OrdinalIgnoreCase) => "Minor",
            _ => "Moderate"
        };

    private static double MapPriorityScore(string? priority) =>
        (priority ?? string.Empty).Trim() switch
        {
            var value when value.Equals("Critical", StringComparison.OrdinalIgnoreCase) => 95,
            var value when value.Equals("High", StringComparison.OrdinalIgnoreCase) => 80,
            var value when value.Equals("Medium", StringComparison.OrdinalIgnoreCase) => 65,
            var value when value.Equals("Low", StringComparison.OrdinalIgnoreCase) => 50,
            _ => 60
        };

    private static bool ContainsFallbackKeyword(string? text, params string[] keywords)
    {
        var normalized = SosPriorityRuleConfigSupport.NormalizeKey(text);
        return keywords.Any(keyword => normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildExecutionFallbackNote(
        MissionExecutionAssessment assessment,
        string fallbackSource)
    {
        var reason = assessment.FailureReason ?? "Mission output was incomplete.";
        return fallbackSource switch
        {
            "draft" => BuildValidationFallbackNote(reason),
            "pipeline_fragments" =>
                $"AI output was incomplete ({reason}). Backend rebuilt a best-effort route from pipeline fragments and marked it for manual review.",
            _ =>
                $"AI output was incomplete ({reason}). Backend built a best-effort route from SOS details and marked it for manual review."
        };
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static List<SuggestedResourceDto> ChooseSuggestedResources(
        IReadOnlyCollection<SuggestedResourceDto>? primary,
        IReadOnlyCollection<SuggestedResourceDto>? fallback)
    {
        if (primary is { Count: > 0 })
            return primary.Select(CloneSuggestedResource).ToList();

        return fallback?.Select(CloneSuggestedResource).ToList() ?? [];
    }

    private static List<SupplyShortageDto> ChooseSupplyShortages(
        IReadOnlyCollection<SupplyShortageDto>? primary,
        IReadOnlyCollection<SupplyShortageDto>? secondary,
        IReadOnlyCollection<SupplyShortageDto>? fallback)
    {
        if (primary is { Count: > 0 })
            return primary.Select(CloneSupplyShortage).ToList();

        if (secondary is { Count: > 0 })
            return secondary.Select(CloneSupplyShortage).ToList();

        return fallback?.Select(CloneSupplyShortage).ToList() ?? [];
    }

    private static SuggestedResourceDto CloneSuggestedResource(SuggestedResourceDto resource)
    {
        return new SuggestedResourceDto
        {
            ResourceType = resource.ResourceType,
            Description = resource.Description,
            Quantity = resource.Quantity,
            Priority = resource.Priority
        };
    }

    private static MissionActivityVictimContext ResolveVictimContext(SosRequestSummary sosRequest)
    {
        if (!string.IsNullOrWhiteSpace(sosRequest.TargetVictimSummary)
            || sosRequest.TargetVictims.Count > 0)
        {
            return new MissionActivityVictimContext
            {
                Summary = sosRequest.TargetVictimSummary,
                Victims = MissionActivityVictimContextHelper.CloneVictims(sosRequest.TargetVictims)
            };
        }

        return MissionActivityVictimContextHelper.BuildContext(sosRequest.StructuredData, sosRequest.Id);
    }

    private static void EnrichVictimTargets(
        List<SuggestedActivityDto> activities,
        IReadOnlyDictionary<int, SosRequestSummary> sosLookup)
    {
        foreach (var activity in activities)
        {
            if (!activity.SosRequestId.HasValue
                || !sosLookup.TryGetValue(activity.SosRequestId.Value, out var sosRequest))
            {
                activity.TargetVictims = [];
                continue;
            }

            var victimContext = ResolveVictimContext(sosRequest);
            activity.TargetVictimSummary = victimContext.Summary;
            activity.TargetVictims = MissionActivityVictimContextHelper.CloneVictims(victimContext.Victims);
            activity.Description = MissionActivityVictimContextHelper.ApplySummaryToDescription(
                activity.ActivityType,
                activity.Description,
                victimContext.Summary);
        }
    }

    private static List<SuggestedActivityDto> ExpandCombinedEvacuations(
        List<SuggestedActivityDto> activities,
        IReadOnlyDictionary<int, SosRequestSummary> sosLookup)
    {
        var expanded = new List<SuggestedActivityDto>(activities.Count);

        foreach (var activity in activities.OrderBy(x => x.Step))
        {
            if (!string.Equals(activity.ActivityType, "EVACUATE", StringComparison.OrdinalIgnoreCase))
            {
                expanded.Add(activity);
                continue;
            }

            var referencedSosIds = GetReferencedSosIds(activity)
                .OrderBy(x => x)
                .ToList();

            if (referencedSosIds.Count <= 1)
            {
                expanded.Add(activity);
                continue;
            }

            foreach (var sosId in referencedSosIds)
            {
                var splitActivity = CloneActivity(activity);
                splitActivity.SosRequestId = sosId;

                if (sosLookup.TryGetValue(sosId, out var sos)
                    && sos.Latitude.HasValue
                    && sos.Longitude.HasValue)
                {
                    splitActivity.Description = $"Đưa nạn nhân từ {sos.Latitude.Value}, {sos.Longitude.Value} (SOS ID {sosId}) đến {splitActivity.AssemblyPointName ?? "điểm tập kết an toàn"}.";
                }
                else
                {
                    splitActivity.Description = $"Đưa nạn nhân của SOS ID {sosId} đến {splitActivity.AssemblyPointName ?? "điểm tập kết an toàn"}.";
                }

                expanded.Add(splitActivity);
            }
        }

        return expanded;
    }

    private static void NormalizeActivitySequence(
        List<SuggestedActivityDto> activities,
        IReadOnlyDictionary<int, SosRequestSummary> sosLookup)
    {
        if (activities.Count <= 1)
            return;

        var expandedActivities = ExpandCombinedEvacuations(activities, sosLookup);

        var indexed = expandedActivities
            .Select((activity, index) => new
            {
                Activity = activity,
                OriginalIndex = index,
                PrimarySosId = GetPrimarySosId(activity),
                GroupPriority = GetSequenceGroupPriority(activity, GetPrimarySosId(activity)),
                GroupKey = BuildSequenceGroupKey(activity, index, GetPrimarySosId(activity))
            })
            .ToList();

        var normalized = indexed
            .GroupBy(item => item.GroupKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Min(item => item.GroupPriority))
            .ThenBy(group => group.Min(item => item.Activity.SuggestedTeam?.TeamId ?? int.MaxValue))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Min(item => item.PrimarySosId ?? int.MaxValue))
            .ThenBy(group => group.Min(item => item.OriginalIndex))
            .SelectMany(group => group
                .OrderBy(item => GetRoutePriority(item.Activity))
                .ThenBy(item => item.PrimarySosId ?? int.MaxValue)
                .ThenBy(item => item.OriginalIndex)
                .Select(item => item.Activity))
            .ToList();

        activities.Clear();
        activities.AddRange(normalized.Distinct().ToList());

        for (var index = 0; index < activities.Count; index++)
            activities[index].Step = index + 1;
    }

    private async Task EnsureReusableReturnActivitiesAsync(
        List<SuggestedActivityDto> activities,
        CancellationToken cancellationToken)
    {
        if (activities.Count == 0)
            return;

        var itemIds = activities
            .SelectMany(activity => activity.SuppliesToCollect ?? [])
            .Where(supply => supply.ItemId.HasValue)
            .Select(supply => supply.ItemId!.Value)
            .Distinct()
            .ToList();

        if (itemIds.Count == 0)
            return;

        var itemLookup = await _itemModelMetadataRepository.GetByIdsAsync(itemIds, cancellationToken);

        var requiredReturnGroups = BuildRequiredReturnGroups(activities, itemLookup);
        var nonReturnActivities = activities
            .Where(activity => !IsReturnActivity(activity))
            .ToList();

        if (requiredReturnGroups.Count == 0)
        {
            if (nonReturnActivities.Count != activities.Count)
            {
                activities.Clear();
                activities.AddRange(nonReturnActivities);

                for (var index = 0; index < activities.Count; index++)
                    activities[index].Step = index + 1;
            }

            return;
        }

        var existingReturnActivities = activities
            .Where(activity => IsReturnActivity(activity) && activity.DepotId.HasValue)
            .GroupBy(activity => (activity.DepotId!.Value, NormalizeTeamId(activity.SuggestedTeam?.TeamId)))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue)
                    .First());

        var normalizedReturnActivities = requiredReturnGroups
            .OrderBy(group => group.Value.FirstCollectStep)
            .ThenBy(group => group.Key.DepotId)
            .ThenBy(group => group.Key.TeamId ?? int.MaxValue)
            .Select(group =>
            {
                existingReturnActivities.TryGetValue(group.Key, out var existingReturnActivity);
                var returnActivity = existingReturnActivity ?? new SuggestedActivityDto();
                ApplyRequiredReturnActivity(returnActivity, group.Value);
                return returnActivity;
            })
            .ToList();

        activities.Clear();
        activities.AddRange(nonReturnActivities);
        activities.AddRange(normalizedReturnActivities);

        for (var index = 0; index < activities.Count; index++)
            activities[index].Step = index + 1;

        _logger.LogInformation(
            "Normalized reusable return activities for AI mission suggestion: RequiredReturnGroups={groupCount}, FinalActivityCount={activityCount}",
            normalizedReturnActivities.Count,
            activities.Count);
    }

    private async Task HydrateSupplyPlanningSnapshotsAsync(
        List<SuggestedActivityDto> activities,
        CancellationToken cancellationToken)
    {
        var collectActivities = activities
            .Where(IsCollectActivity)
            .Where(activity => activity.DepotId.HasValue && activity.SuppliesToCollect is { Count: > 0 })
            .OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue)
            .ToList();

        foreach (var activity in collectActivities)
        {
            var itemsToPreview = activity.SuppliesToCollect!
                .Where(supply => supply.ItemId.HasValue && supply.Quantity > 0)
                .Where(supply => supply.PlannedPickupLotAllocations is not { Count: > 0 }
                    && supply.PlannedPickupReusableUnits is not { Count: > 0 })
                .GroupBy(supply => supply.ItemId!.Value)
                .Select(group => (ItemModelId: group.Key, Quantity: group.Sum(supply => supply.Quantity)))
                .ToList();

            if (itemsToPreview.Count == 0)
                continue;

            try
            {
                var preview = await _depotInventoryRepository.PreviewReserveSuppliesAsync(
                    activity.DepotId!.Value,
                    itemsToPreview,
                    cancellationToken);

                ApplyPreviewReservationSnapshot(activity, preview);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to hydrate AI suggestion supply planning snapshot for ActivityStep={Step}, DepotId={DepotId}.",
                    activity.Step,
                    activity.DepotId);
            }
        }

        HydrateReturnSuppliesFromCollectSnapshots(activities);
    }

    private static void ApplyPreviewReservationSnapshot(
        SuggestedActivityDto activity,
        MissionSupplyReservationResult preview)
    {
        if (activity.SuppliesToCollect is not { Count: > 0 } || preview.Items.Count == 0)
            return;

        var previewByItem = preview.Items
            .GroupBy(item => item.ItemModelId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var supply in activity.SuppliesToCollect.Where(supply => supply.ItemId.HasValue))
        {
            if (!previewByItem.TryGetValue(supply.ItemId!.Value, out var item))
                continue;

            if (string.IsNullOrWhiteSpace(supply.ItemName))
                supply.ItemName = item.ItemName;

            supply.Unit ??= item.Unit;

            if (supply.PlannedPickupLotAllocations is not { Count: > 0 } && item.LotAllocations.Count > 0)
                supply.PlannedPickupLotAllocations = item.LotAllocations.Select(lot => CloneLot(lot)).ToList();

            if (supply.PlannedPickupReusableUnits is not { Count: > 0 } && item.ReusableUnits.Count > 0)
                supply.PlannedPickupReusableUnits = item.ReusableUnits.Select(CloneReusableUnit).ToList();
        }
    }

    private static void HydrateReturnSuppliesFromCollectSnapshots(List<SuggestedActivityDto> activities)
    {
        var collectActivities = activities
            .Where(IsCollectActivity)
            .Where(activity => activity.DepotId.HasValue && activity.SuppliesToCollect is { Count: > 0 })
            .OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue)
            .ToList();

        if (collectActivities.Count == 0)
            return;

        foreach (var returnActivity in activities
            .Where(IsReturnActivity)
            .Where(activity => activity.DepotId.HasValue && activity.SuppliesToCollect is { Count: > 0 }))
        {
            var returnTeamId = NormalizeTeamId(returnActivity.SuggestedTeam?.TeamId);
            var matchingCollects = collectActivities
                .Where(collect => collect.DepotId == returnActivity.DepotId
                    && NormalizeTeamId(collect.SuggestedTeam?.TeamId) == returnTeamId)
                .ToList();

            if (matchingCollects.Count == 0)
                continue;

            foreach (var returnSupply in returnActivity.SuppliesToCollect!.Where(supply => supply.ItemId.HasValue))
            {
                var sourceLots = new List<SupplyExecutionLotDto>();
                var sourceUnits = new List<SupplyExecutionReusableUnitDto>();

                foreach (var collectSupply in matchingCollects
                    .SelectMany(collect => collect.SuppliesToCollect ?? [])
                    .Where(supply => supply.ItemId == returnSupply.ItemId))
                {
                    var pickupLots = collectSupply.PickupLotAllocations is { Count: > 0 }
                        ? collectSupply.PickupLotAllocations
                        : collectSupply.PlannedPickupLotAllocations;

                    if (pickupLots is { Count: > 0 })
                        sourceLots.AddRange(pickupLots.Select(lot => CloneLot(lot)));

                    var pickedUnits = collectSupply.PickedReusableUnits is { Count: > 0 }
                        ? collectSupply.PickedReusableUnits
                        : collectSupply.PlannedPickupReusableUnits;

                    if (pickedUnits is { Count: > 0 })
                        sourceUnits.AddRange(pickedUnits.Select(CloneReusableUnit));
                }

                if (returnSupply.ExpectedReturnLotAllocations is not { Count: > 0 } && sourceLots.Count > 0)
                {
                    returnSupply.ExpectedReturnLotAllocations = TakeLotQuantity(
                        sourceLots,
                        returnSupply.Quantity);
                }

                if (returnSupply.ExpectedReturnUnits is not { Count: > 0 } && sourceUnits.Count > 0)
                {
                    var unitLimit = returnSupply.Quantity > 0 ? returnSupply.Quantity : int.MaxValue;
                    returnSupply.ExpectedReturnUnits = sourceUnits
                        .GroupBy(unit => unit.ReusableItemId)
                        .Select(group => CloneReusableUnit(group.First()))
                        .Take(unitLimit)
                        .ToList();
                }
            }
        }
    }

    private static void EnsureReturnAssemblyPointActivities(RescueMissionSuggestionResult result)
    {
        var activities = result.SuggestedActivities;
        if (activities.Count == 0)
            return;

        var teamsById = new Dictionary<int, SuggestedTeamDto>();
        void AddTeam(SuggestedTeamDto? team)
        {
            if (team is null || team.TeamId <= 0 || teamsById.ContainsKey(team.TeamId))
                return;

            teamsById[team.TeamId] = CloneSuggestedTeam(team)!;
        }

        AddTeam(result.SuggestedTeam);
        foreach (var activity in activities)
            AddTeam(activity.SuggestedTeam);

        if (teamsById.Count == 0)
            return;

        var warnings = new List<string>();
        var existingReturnActivities = activities
            .Where(IsReturnAssemblyPointActivity)
            .Where(activity => NormalizeTeamId(activity.SuggestedTeam?.TeamId).HasValue)
            .GroupBy(activity => NormalizeTeamId(activity.SuggestedTeam?.TeamId)!.Value)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var ordered = group
                        .OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue)
                        .ToList();

                    if (ordered.Count > 1)
                        warnings.Add($"Mission suggestion đang có nhiều RETURN_ASSEMBLY_POINT cho team #{group.Key}; backend chỉ giữ một bước cuối.");

                    return ordered.First();
                });

        var returnActivities = new List<SuggestedActivityDto>();
        foreach (var team in teamsById.Values.OrderBy(team => team.TeamName).ThenBy(team => team.TeamId))
        {
            if (!team.AssemblyPointId.HasValue
                || !team.Latitude.HasValue
                || !team.Longitude.HasValue)
            {
                warnings.Add($"Team #{team.TeamId} thiếu assembly_point_id hoặc tọa độ điểm tập kết; chưa thể tự tạo RETURN_ASSEMBLY_POINT.");
                continue;
            }

            existingReturnActivities.TryGetValue(team.TeamId, out var existingReturnActivity);
            var returnActivity = existingReturnActivity ?? new SuggestedActivityDto();
            ApplyReturnAssemblyPointActivity(returnActivity, team);
            returnActivities.Add(returnActivity);
        }

        if (warnings.Count > 0)
        {
            result.NeedsManualReview = true;
            result.SpecialNotes = AppendSpecialNote(result.SpecialNotes, string.Join(Environment.NewLine, warnings));
        }

        var nonReturnAssemblyActivities = activities
            .Where(activity => !IsReturnAssemblyPointActivity(activity))
            .ToList();

        activities.Clear();
        activities.AddRange(nonReturnAssemblyActivities);
        activities.AddRange(returnActivities);

        for (var index = 0; index < activities.Count; index++)
            activities[index].Step = index + 1;
    }

    private static void ApplyReturnAssemblyPointActivity(
        SuggestedActivityDto activity,
        SuggestedTeamDto team)
    {
        var assemblyPointName = string.IsNullOrWhiteSpace(team.AssemblyPointName)
            ? $"điểm tập kết #{team.AssemblyPointId}"
            : team.AssemblyPointName!;

        activity.Step = 0;
        activity.ActivityType = ReturnAssemblyPointActivityType;
        activity.Description = $"Hoàn tất nhiệm vụ, đội {team.TeamName} quay về điểm tập kết {assemblyPointName}.";
        activity.Priority = "Low";
        activity.EstimatedTime = DefaultReturnAssemblyEstimatedTime;
        activity.ExecutionMode = SingleTeamExecutionMode;
        activity.RequiredTeamCount = 1;
        activity.CoordinationGroupKey = null;
        activity.CoordinationNotes = "Đội quay về điểm tập kết ban đầu sau khi hoàn tất nhiệm vụ.";
        activity.SosRequestId = null;
        activity.DepotId = null;
        activity.DepotName = null;
        activity.DepotAddress = null;
        activity.AssemblyPointId = team.AssemblyPointId;
        activity.AssemblyPointName = team.AssemblyPointName;
        activity.AssemblyPointLatitude = team.Latitude;
        activity.AssemblyPointLongitude = team.Longitude;
        activity.DestinationName = assemblyPointName;
        activity.DestinationLatitude = team.Latitude;
        activity.DestinationLongitude = team.Longitude;
        activity.SuppliesToCollect = null;
        activity.SuggestedTeam = CloneSuggestedTeam(team);
    }

    private static Dictionary<(int DepotId, int? TeamId), RequiredReturnGroup> BuildRequiredReturnGroups(
        IEnumerable<SuggestedActivityDto> activities,
        IReadOnlyDictionary<int, RESQ.Domain.Entities.Logistics.ItemModelRecord> itemLookup)
    {
        var requiredGroups = new Dictionary<(int DepotId, int? TeamId), RequiredReturnGroup>();

        foreach (var activity in activities.OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue))
        {
            if (!IsCollectActivity(activity)
                || !activity.DepotId.HasValue
                || activity.SuppliesToCollect is not { Count: > 0 })
            {
                continue;
            }

            var reusableSupplies = activity.SuppliesToCollect
                .Where(supply => supply.ItemId.HasValue
                    && supply.Quantity > 0
                    && itemLookup.TryGetValue(supply.ItemId.Value, out var item)
                    && string.Equals(item.ItemType, ReusableItemType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (reusableSupplies.Count == 0)
                continue;

            var key = (activity.DepotId.Value, NormalizeTeamId(activity.SuggestedTeam?.TeamId));
            if (!requiredGroups.TryGetValue(key, out var requiredGroup))
            {
                requiredGroup = new RequiredReturnGroup
                {
                    DepotId = activity.DepotId.Value,
                    TeamId = key.Item2,
                    FirstCollectStep = activity.Step > 0 ? activity.Step : int.MaxValue,
                    Priority = activity.Priority,
                    EstimatedTime = activity.EstimatedTime,
                    DepotName = activity.DepotName,
                    DepotAddress = activity.DepotAddress,
                    SuggestedTeam = CloneSuggestedTeam(activity.SuggestedTeam)
                };

                requiredGroups[key] = requiredGroup;
            }
            else
            {
                requiredGroup.FirstCollectStep = Math.Min(
                    requiredGroup.FirstCollectStep,
                    activity.Step > 0 ? activity.Step : int.MaxValue);
                requiredGroup.Priority = SelectHigherPriority(requiredGroup.Priority, activity.Priority);
                requiredGroup.EstimatedTime ??= activity.EstimatedTime;
                requiredGroup.DepotName ??= activity.DepotName;
                requiredGroup.DepotAddress ??= activity.DepotAddress;
                requiredGroup.SuggestedTeam ??= CloneSuggestedTeam(activity.SuggestedTeam);
            }

            foreach (var supply in reusableSupplies)
            {
                var itemId = supply.ItemId!.Value;
                if (requiredGroup.Supplies.TryGetValue(itemId, out var existingSupply))
                {
                    existingSupply.Quantity += supply.Quantity;
                    existingSupply.ItemName = string.IsNullOrWhiteSpace(existingSupply.ItemName)
                        ? supply.ItemName ?? itemLookup[itemId].Name
                        : existingSupply.ItemName;
                    existingSupply.Unit ??= supply.Unit ?? itemLookup[itemId].Unit;
                    continue;
                }

                requiredGroup.Supplies[itemId] = new SupplyToCollectDto
                {
                    ItemId = itemId,
                    ItemName = string.IsNullOrWhiteSpace(supply.ItemName)
                        ? itemLookup[itemId].Name
                        : supply.ItemName,
                    Quantity = supply.Quantity,
                    Unit = supply.Unit ?? itemLookup[itemId].Unit
                };
            }
        }

        return requiredGroups;
    }

    private static void ApplyRequiredReturnActivity(
        SuggestedActivityDto activity,
        RequiredReturnGroup requiredGroup)
    {
        activity.Step = 0;
        activity.ActivityType = ReturnSuppliesActivityType;
        activity.Description = BuildReturnDescription(requiredGroup);
        activity.Priority = requiredGroup.Priority;
        activity.EstimatedTime = requiredGroup.EstimatedTime;
        activity.ExecutionMode = SingleTeamExecutionMode;
        activity.RequiredTeamCount = 1;
        activity.CoordinationGroupKey = null;
        activity.CoordinationNotes = "Một đội trả vật phẩm tái sử dụng đã lấy trước đó về lại kho nguồn.";
        activity.SosRequestId = null;
        activity.DepotId = requiredGroup.DepotId;
        activity.DepotName = requiredGroup.DepotName;
        activity.DepotAddress = requiredGroup.DepotAddress;
        activity.AssemblyPointId = null;
        activity.AssemblyPointName = null;
        activity.AssemblyPointLatitude = null;
        activity.AssemblyPointLongitude = null;
        activity.SuppliesToCollect = requiredGroup.Supplies.Values
            .OrderBy(supply => supply.ItemName)
            .Select(CloneSupply)
            .ToList();
        activity.SuggestedTeam = CloneSuggestedTeam(requiredGroup.SuggestedTeam);
    }

    private static string BuildReturnDescription(RequiredReturnGroup requiredGroup)
    {
        var depotLabel = string.IsNullOrWhiteSpace(requiredGroup.DepotName)
            ? $"kho #{requiredGroup.DepotId}"
            : requiredGroup.DepotName;

        var itemSummary = string.Join(
            ", ",
            requiredGroup.Supplies.Values
                .OrderBy(supply => supply.ItemName)
                .Select(supply =>
                {
                    var unitSuffix = string.IsNullOrWhiteSpace(supply.Unit)
                        ? string.Empty
                        : $" {supply.Unit}";
                    return $"{supply.ItemName} x{supply.Quantity}{unitSuffix}";
                }));

        return string.IsNullOrWhiteSpace(itemSummary)
            ? $"Hoàn tất nhiệm vụ, đưa vật phẩm tái sử dụng về lại {depotLabel}."
            : $"Hoàn tất nhiệm vụ, đưa vật phẩm tái sử dụng về lại {depotLabel}. Trả: {itemSummary}.";
    }

    private static SupplyToCollectDto CloneSupply(SupplyToCollectDto supply)
    {
        return new SupplyToCollectDto
        {
            ItemId = supply.ItemId,
            ItemName = supply.ItemName,
            ImageUrl = supply.ImageUrl,
            Quantity = supply.Quantity,
            Unit = supply.Unit,
            PlannedPickupLotAllocations = supply.PlannedPickupLotAllocations?.Select(lot => CloneLot(lot)).ToList(),
            PlannedPickupReusableUnits = supply.PlannedPickupReusableUnits?.Select(CloneReusableUnit).ToList(),
            PickupLotAllocations = supply.PickupLotAllocations?.Select(lot => CloneLot(lot)).ToList(),
            PickedReusableUnits = supply.PickedReusableUnits?.Select(CloneReusableUnit).ToList(),
            AvailableDeliveryLotAllocations = supply.AvailableDeliveryLotAllocations?.Select(lot => CloneLot(lot)).ToList(),
            AvailableDeliveryReusableUnits = supply.AvailableDeliveryReusableUnits?.Select(CloneReusableUnit).ToList(),
            DeliveredLotAllocations = supply.DeliveredLotAllocations?.Select(lot => CloneLot(lot)).ToList(),
            DeliveredReusableUnits = supply.DeliveredReusableUnits?.Select(CloneReusableUnit).ToList(),
            ExpectedReturnLotAllocations = supply.ExpectedReturnLotAllocations?.Select(lot => CloneLot(lot)).ToList(),
            ExpectedReturnUnits = supply.ExpectedReturnUnits?.Select(CloneReusableUnit).ToList(),
            ReturnedLotAllocations = supply.ReturnedLotAllocations?.Select(lot => CloneLot(lot)).ToList(),
            ReturnedReusableUnits = supply.ReturnedReusableUnits?.Select(CloneReusableUnit).ToList(),
            ActualReturnedQuantity = supply.ActualReturnedQuantity,
            BufferRatio = supply.BufferRatio,
            BufferQuantity = supply.BufferQuantity,
            BufferUsedQuantity = supply.BufferUsedQuantity,
            BufferUsedReason = supply.BufferUsedReason,
            ActualDeliveredQuantity = supply.ActualDeliveredQuantity
        };
    }

    private static List<SupplyExecutionLotDto> TakeLotQuantity(
        IEnumerable<SupplyExecutionLotDto> lots,
        int requestedQuantity)
    {
        var remaining = requestedQuantity;
        var result = new List<SupplyExecutionLotDto>();

        foreach (var lot in lots.Where(lot => lot.QuantityTaken > 0))
        {
            if (requestedQuantity > 0 && remaining <= 0)
                break;

            var quantity = requestedQuantity > 0
                ? Math.Min(lot.QuantityTaken, remaining)
                : lot.QuantityTaken;

            remaining -= quantity;
            result.Add(CloneLot(lot, quantity));
        }

        return result;
    }

    private static SupplyExecutionLotDto CloneLot(SupplyExecutionLotDto lot) =>
        CloneLot(lot, lot.QuantityTaken);

    private static SupplyExecutionLotDto CloneLot(SupplyExecutionLotDto lot, int quantityTaken)
    {
        return new SupplyExecutionLotDto
        {
            LotId = lot.LotId,
            QuantityTaken = quantityTaken,
            ReceivedDate = lot.ReceivedDate,
            ExpiredDate = lot.ExpiredDate,
            RemainingQuantityAfterExecution = lot.RemainingQuantityAfterExecution
        };
    }

    private static SupplyExecutionReusableUnitDto CloneReusableUnit(SupplyExecutionReusableUnitDto unit)
    {
        return new SupplyExecutionReusableUnitDto
        {
            ReusableItemId = unit.ReusableItemId,
            ItemModelId = unit.ItemModelId,
            ItemName = unit.ItemName,
            SerialNumber = unit.SerialNumber,
            Condition = unit.Condition,
            Note = unit.Note
        };
    }

    private static SupplyShortageDto CloneSupplyShortage(SupplyShortageDto shortage)
    {
        return new SupplyShortageDto
        {
            SosRequestId = shortage.SosRequestId,
            ItemId = shortage.ItemId,
            ItemName = shortage.ItemName,
            Unit = shortage.Unit,
            SelectedDepotId = shortage.SelectedDepotId,
            SelectedDepotName = shortage.SelectedDepotName,
            NeededQuantity = shortage.NeededQuantity,
            AvailableQuantity = shortage.AvailableQuantity,
            MissingQuantity = shortage.MissingQuantity,
            Notes = shortage.Notes
        };
    }

    private static SuggestedTeamDto? CloneSuggestedTeam(SuggestedTeamDto? team)
    {
        return team == null
            ? null
            : new SuggestedTeamDto
            {
                TeamId = team.TeamId,
                TeamName = team.TeamName,
                TeamType = team.TeamType,
                Reason = team.Reason,
                AssemblyPointId = team.AssemblyPointId,
                AssemblyPointName = team.AssemblyPointName,
                Latitude = team.Latitude,
                Longitude = team.Longitude,
                DistanceKm = team.DistanceKm
            };
    }

    private static int? NormalizeTeamId(int? teamId) =>
        teamId.HasValue && teamId.Value > 0 ? teamId.Value : null;

    private static string? SelectHigherPriority(string? currentPriority, string? candidatePriority)
    {
        if (string.IsNullOrWhiteSpace(currentPriority))
            return candidatePriority;

        if (string.IsNullOrWhiteSpace(candidatePriority))
            return currentPriority;

        return GetPriorityRank(candidatePriority) > GetPriorityRank(currentPriority)
            ? candidatePriority
            : currentPriority;
    }

    private static int GetPriorityRank(string? priority) =>
        (priority ?? string.Empty).Trim() switch
        {
            var value when value.Equals("Critical", StringComparison.OrdinalIgnoreCase) => 4,
            var value when value.Equals("High", StringComparison.OrdinalIgnoreCase) => 3,
            var value when value.Equals("Medium", StringComparison.OrdinalIgnoreCase) => 2,
            var value when value.Equals("Low", StringComparison.OrdinalIgnoreCase) => 1,
            _ => 0
        };

    private sealed class RequiredReturnGroup
    {
        public int DepotId { get; init; }
        public int? TeamId { get; init; }
        public int FirstCollectStep { get; set; } = int.MaxValue;
        public string? Priority { get; set; }
        public string? EstimatedTime { get; set; }
        public string? DepotName { get; set; }
        public string? DepotAddress { get; set; }
        public SuggestedTeamDto? SuggestedTeam { get; set; }
        public Dictionary<int, SupplyToCollectDto> Supplies { get; } = [];
    }

    private async Task EnrichActivitiesWithAssemblyPointsAsync(
        RescueMissionSuggestionResult result,
        IReadOnlyDictionary<int, SosRequestSummary> sosLookup,
        CancellationToken cancellationToken)
    {
        var assemblyPoints = await _assemblyPointRepository.GetAllAsync(cancellationToken);
        var activeAssemblyPoints = assemblyPoints
            .Where(a => a.Status == AssemblyPointStatus.Available && a.Location is not null)
            .ToList();

        var assemblyPointIds = result.SuggestedActivities
            .Where(a => a.AssemblyPointId.HasValue)
            .Select(a => a.AssemblyPointId!.Value)
            .Distinct()
            .ToList();

        var lookup = assemblyPoints
            .Where(a => assemblyPointIds.Contains(a.Id))
            .ToDictionary(a => a.Id);

        foreach (var activity in result.SuggestedActivities)
        {
            if ((string.Equals(activity.ActivityType, "RESCUE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(activity.ActivityType, "EVACUATE", StringComparison.OrdinalIgnoreCase))
                && !activity.AssemblyPointId.HasValue)
            {
                var coordinates = ResolveActivityCoordinates(activity, sosLookup);
                if (coordinates.HasValue && activeAssemblyPoints.Count > 0)
                {
                    var nearest = activeAssemblyPoints
                        .OrderBy(a => HaversineKm(
                            coordinates.Value.Latitude,
                            coordinates.Value.Longitude,
                            a.Location!.Latitude,
                            a.Location.Longitude))
                        .First();

                    activity.AssemblyPointId = nearest.Id;
                    lookup[nearest.Id] = nearest;
                }
            }

            if (!activity.AssemblyPointId.HasValue || !lookup.TryGetValue(activity.AssemblyPointId.Value, out var assemblyPoint))
                continue;

            activity.AssemblyPointName = assemblyPoint.Name;
            activity.AssemblyPointLatitude = assemblyPoint.Location?.Latitude;
            activity.AssemblyPointLongitude = assemblyPoint.Location?.Longitude;
        }
    }

    private static void BackfillItemIds(List<SuggestedActivityDto> activities, List<DepotSummary> depots)
    {
        var itemLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var depot in depots)
        {
            foreach (var inventory in depot.Inventories)
            {
                if (inventory.ItemId.HasValue && !string.IsNullOrWhiteSpace(inventory.ItemName))
                    itemLookup.TryAdd(NormalizeItemName(inventory.ItemName), inventory.ItemId.Value);
            }
        }

        if (itemLookup.Count == 0)
            return;

        foreach (var activity in activities)
        {
            if (activity.SuppliesToCollect is null)
                continue;

            foreach (var supply in activity.SuppliesToCollect)
            {
                if (supply.ItemId.HasValue || string.IsNullOrWhiteSpace(supply.ItemName))
                    continue;

                var normalized = NormalizeItemName(supply.ItemName);
                if (itemLookup.TryGetValue(normalized, out var exactId))
                {
                    supply.ItemId = exactId;
                    continue;
                }

                foreach (var (key, id) in itemLookup)
                {
                    if (normalized.Contains(key, StringComparison.OrdinalIgnoreCase)
                        || key.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        supply.ItemId = id;
                        break;
                    }
                }
            }
        }
    }

    private async Task BackfillInventoryBackedItemIdsAsync(
        List<SuggestedActivityDto> activities,
        CancellationToken cancellationToken)
    {
        var pendingSupplies = activities
            .Where(activity => activity.DepotId.HasValue && activity.SuppliesToCollect is { Count: > 0 })
            .SelectMany(activity => activity.SuppliesToCollect!
                .Where(supply => !supply.ItemId.HasValue && !string.IsNullOrWhiteSpace(supply.ItemName))
                .Select(supply => new
                {
                    Activity = activity,
                    Supply = supply,
                    DepotId = activity.DepotId!.Value,
                    NormalizedName = NormalizeItemName(supply.ItemName)
                }))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.NormalizedName))
            .GroupBy(entry => new { entry.DepotId, entry.NormalizedName })
            .ToList();

        foreach (var group in pendingSupplies)
        {
            var sample = group.First();
            var (items, _) = await _depotInventoryRepository.SearchForAgentAsync(
                string.Empty,
                sample.Supply.ItemName,
                page: 1,
                pageSize: AgentPageSize * 5,
                allowedDepotIds: [group.Key.DepotId],
                ct: cancellationToken);

            var matchedItem = items
                .Where(item => item.DepotId == group.Key.DepotId)
                .FirstOrDefault(item =>
                {
                    var normalizedInventoryName = NormalizeItemName(item.ItemName);
                    return string.Equals(normalizedInventoryName, group.Key.NormalizedName, StringComparison.OrdinalIgnoreCase)
                        || normalizedInventoryName.Contains(group.Key.NormalizedName, StringComparison.OrdinalIgnoreCase)
                        || group.Key.NormalizedName.Contains(normalizedInventoryName, StringComparison.OrdinalIgnoreCase);
                });

            if (matchedItem is null)
                continue;

            foreach (var entry in group)
            {
                entry.Supply.ItemId ??= matchedItem.ItemId;
                entry.Supply.Unit ??= matchedItem.Unit;
                entry.Activity.DepotName ??= matchedItem.DepotName;
                entry.Activity.DepotAddress ??= matchedItem.DepotAddress;

                if (IsCollectActivity(entry.Activity) || IsReturnActivity(entry.Activity))
                {
                    entry.Activity.DestinationName ??= matchedItem.DepotName;
                    entry.Activity.DestinationLatitude ??= matchedItem.DepotLatitude;
                    entry.Activity.DestinationLongitude ??= matchedItem.DepotLongitude;
                }
            }
        }
    }

    private static void ApplyMixedRescueReliefSafetyNote(
        RescueMissionSuggestionResult result,
        IReadOnlyDictionary<int, SosRequestSummary> sosLookup)
    {
        var warning = MissionSuggestionWarningHelper.BuildMixedRescueReliefWarning(
            result.SuggestedActivities,
            sosLookup);
        if (string.IsNullOrWhiteSpace(warning))
            return;

        result.NeedsManualReview = true;
        result.MixedRescueReliefWarning = warning;
    }

    private static void ApplyMixedMissionMissingAiAnalysisManualReview(
        RescueMissionSuggestionResult result,
        IReadOnlyDictionary<int, SosRequestSummary> sosLookup)
    {
        var hasRescueBranch = result.SuggestedActivities.Any(activity =>
            string.Equals(activity.ActivityType, "RESCUE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(activity.ActivityType, "EVACUATE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(activity.ActivityType, "MEDICAL_AID", StringComparison.OrdinalIgnoreCase));
        var hasReliefBranch = result.SuggestedActivities.Any(activity =>
            string.Equals(activity.ActivityType, CollectSuppliesActivityType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase));

        if (!hasRescueBranch || !hasReliefBranch)
            return;

        var missingAnalysisIds = result.SuggestedActivities
            .Where(activity =>
                string.Equals(activity.ActivityType, "RESCUE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(activity.ActivityType, "EVACUATE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(activity.ActivityType, "MEDICAL_AID", StringComparison.OrdinalIgnoreCase))
            .Where(activity => activity.SosRequestId.HasValue)
            .Select(activity => activity.SosRequestId!.Value)
            .Distinct()
            .Where(id =>
                !sosLookup.TryGetValue(id, out var sos)
                || sos.AiAnalysis?.HasAiAnalysis != true)
            .OrderBy(id => id)
            .ToList();

        if (missingAnalysisIds.Count == 0)
            return;

        result.NeedsManualReview = true;
        result.SpecialNotes = AppendSpecialNote(
            result.SpecialNotes,
            $"Coordinator review required: missing SOS AI analysis from raw_message for {string.Join(", ", missingAnalysisIds.Select(id => $"SOS #{id}"))} while this cluster mixes rescue and relief.");
    }

    private static void BackfillSosRequestIds(List<SuggestedActivityDto> activities, List<SosRequestSummary> sosRequests)
    {
        if (sosRequests.Count == 0)
            return;

        if (sosRequests.Count == 1)
        {
            var sosId = sosRequests[0].Id;
            foreach (var activity in activities)
                activity.SosRequestId ??= sosId;
            return;
        }

        var sosWithCoordinates = sosRequests
            .Where(sos => sos.Latitude.HasValue && sos.Longitude.HasValue)
            .ToList();

        var fallbackSos = sosRequests
            .OrderByDescending(sos => GetPriorityRank(sos.PriorityLevel))
            .First();

        foreach (var activity in activities)
        {
            if (activity.SosRequestId.HasValue)
                continue;

            if (sosWithCoordinates.Count > 0 && !string.IsNullOrWhiteSpace(activity.Description))
            {
                var match = CoordinateRegex.Match(activity.Description);
                if (match.Success
                    && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var latitude)
                    && double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var longitude))
                {
                    var nearestSos = sosWithCoordinates
                        .OrderBy(sos => HaversineKm(latitude, longitude, sos.Latitude!.Value, sos.Longitude!.Value))
                        .First();
                    activity.SosRequestId = nearestSos.Id;
                    continue;
                }
            }

            activity.SosRequestId = fallbackSos.Id;
        }
    }

    private static string NormalizeItemName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var normalized = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousWasSpace = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            var folded = character switch
            {
                '\u0111' => 'd',
                '\u0110' => 'd',
                _ => char.ToLowerInvariant(character)
            };

            if (char.IsLetterOrDigit(folded))
            {
                builder.Append(folded);
                previousWasSpace = false;
                continue;
            }

            if (previousWasSpace)
                continue;

            builder.Append(' ');
            previousWasSpace = true;
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Populates DestinationLatitude / DestinationLongitude / DestinationName on each activity
    /// from structured context data (depots and SOS requests), then cleans up descriptions so
    /// that named destinations are shown by name rather than raw coordinates.
    /// Falls back to a DB lookup when the depot picked by the AI is not already present in the
    /// scoped nearby-depot context used for the current mission suggestion.
    /// </summary>
    private async Task BackfillDestinationInfoAsync(
        List<SuggestedActivityDto> activities,
        List<DepotSummary> nearbyDepots,
        List<SosRequestSummary> sosRequests,
        CancellationToken cancellationToken)
    {
        var depotMap = nearbyDepots.ToDictionary(d => d.Id);
        var sosMap   = sosRequests
            .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
            .ToDictionary(s => s.Id);

        // Collect depot IDs that are used by supply activities but not in nearbyDepots
        // so we can batch-load their coordinates from DB.
        var missingDepotIds = activities
            .Where(a => (a.ActivityType is "COLLECT_SUPPLIES" or "RETURN_SUPPLIES")
                        && !a.DestinationLatitude.HasValue
                        && a.DepotId.HasValue
                        && !depotMap.ContainsKey(a.DepotId.Value))
            .Select(a => a.DepotId!.Value)
            .Distinct()
            .ToList();

        // Batch DB lookups for missing depots
        foreach (var depotId in missingDepotIds)
        {
            var loc = await _depotInventoryRepository.GetDepotLocationAsync(depotId, cancellationToken);
            if (loc.HasValue)
            {
                // Synthesise a minimal DepotSummary so the switch below can use it uniformly
                depotMap[depotId] = new DepotSummary
                {
                    Id        = depotId,
                    Name      = activities.First(a => a.DepotId == depotId).DepotName ?? string.Empty,
                    Latitude  = loc.Value.Latitude,
                    Longitude = loc.Value.Longitude
                };
            }
        }

        foreach (var activity in activities)
        {
            switch (activity.ActivityType?.ToUpperInvariant())
            {
                case "COLLECT_SUPPLIES":
                case "RETURN_SUPPLIES":
                    if (!activity.DestinationLatitude.HasValue
                        && activity.DepotId.HasValue
                        && depotMap.TryGetValue(activity.DepotId.Value, out var depot))
                    {
                        activity.DestinationLatitude  = depot.Latitude;
                        activity.DestinationLongitude = depot.Longitude;
                        activity.DestinationName    ??= depot.Name;
                    }
                    break;

                case "DELIVER_SUPPLIES":
                case "RESCUE":
                case "MEDICAL_AID":
                    if (!activity.DestinationLatitude.HasValue
                        && activity.SosRequestId.HasValue
                        && sosMap.TryGetValue(activity.SosRequestId.Value, out var sos))
                    {
                        activity.DestinationLatitude  = sos.Latitude;
                        activity.DestinationLongitude = sos.Longitude;
                        // On-site SOS activities have no human-readable destination name
                    }
                    break;

                case "EVACUATE":
                    if (!activity.DestinationLatitude.HasValue && activity.AssemblyPointLatitude.HasValue)
                    {
                        activity.DestinationLatitude  = activity.AssemblyPointLatitude;
                        activity.DestinationLongitude = activity.AssemblyPointLongitude;
                        activity.DestinationName    ??= activity.AssemblyPointName;
                    }
                    break;
            }

            // When the destination has a name, ensure the description shows the name rather
            // than raw coordinate pairs - coordinates are still available on the DTO fields.
            if (!string.IsNullOrEmpty(activity.DestinationName)
                && activity.DestinationLatitude.HasValue
                && activity.DestinationLongitude.HasValue)
            {
                activity.Description = ReplaceDestinationCoordinatesWithName(
                    activity.Description,
                    activity.DestinationLatitude.Value,
                    activity.DestinationLongitude.Value,
                    activity.DestinationName);
            }
        }
    }

    // Matches bare or parenthesised coordinate pairs, e.g. "10.123, 106.456" or "(10.123, 106.456)".
    private static readonly Regex DestCoordRegex =
        new(@"\(?\s*(-?\d{1,3}\.\d+)\s*[,,]\s*(-?\d{1,3}\.\d+)\s*\)?", RegexOptions.Compiled);

    private static string ReplaceDestinationCoordinatesWithName(
        string description, double lat, double lon, string name)
    {
        if (string.IsNullOrEmpty(description)) return description;

        bool nameAlreadyPresent = description.Contains(name, StringComparison.OrdinalIgnoreCase);

        var result = DestCoordRegex.Replace(description, m =>
        {
            if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mLat)
                || !double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mLon))
                return m.Value;

            // Only replace when these coordinates are close enough to the destination (~1 km / ~0.01°)
            if (Math.Abs(mLat - lat) > 0.01 || Math.Abs(mLon - lon) > 0.01)
                return m.Value;

            // If name is already in the description → just remove the duplicate coordinates.
            // Otherwise → replace the coordinate pair with the name.
            return nameAlreadyPresent ? string.Empty : name;
        });

        // Clean up double spaces or leading/trailing whitespace left after removal
        return Regex.Replace(result, @"  +", " ").Trim();
    }

    private bool ShouldUsePipeline(MissionSuggestionExecutionOptions options)
    {
        if (options.PromptOverride?.PromptType == PromptType.MissionPlanning)
            return false;

        return options.PromptOverride is not null || _pipelineOptions.UseMissionSuggestionPipeline;
    }

    private async Task<PromptModel?> GetLegacyPromptAsync(
        MissionSuggestionExecutionOptions options,
        CancellationToken cancellationToken)
    {
        if (options.PromptOverride?.PromptType == PromptType.MissionPlanning)
            return options.PromptOverride;

        return await _promptRepository.GetActiveByTypeAsync(PromptType.MissionPlanning, cancellationToken);
    }

    private async Task<AiConfigModel?> GetEffectiveAiConfigAsync(
        MissionSuggestionExecutionOptions options,
        CancellationToken cancellationToken)
    {
        if (options.AiConfigOverride is not null)
            return options.AiConfigOverride;

        return await _aiConfigRepository.GetActiveAsync(cancellationToken);
    }
    // --- SSE helpers -----------------------------------------------------------

    private static SseMissionEvent Status(string msg) =>
        new() { EventType = "status", Data = msg };

    private static SseMissionEvent Error(string msg) =>
        new() { EventType = "error", Data = msg };

    // --- Streaming (SSE agent loop) --------------------------------------------

    public async IAsyncEnumerable<SseMissionEvent> GenerateSuggestionStreamAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots = null,
        List<AgentTeamInfo>? nearbyTeams = null,
        bool isMultiDepotRecommended = false,
        int? clusterId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var evt in GenerateSuggestionStreamCoreAsync(
            sosRequests,
            nearbyDepots,
            nearbyTeams,
            isMultiDepotRecommended,
            clusterId,
            MissionSuggestionExecutionOptions.Persisted,
            cancellationToken))
        {
            yield return evt;
        }
    }

    private async IAsyncEnumerable<SseMissionEvent> GenerateSuggestionStreamCoreAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots,
        List<AgentTeamInfo>? nearbyTeams,
        bool isMultiDepotRecommended,
        int? clusterId,
        MissionSuggestionExecutionOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var availableNearbyTeams = nearbyTeams ?? [];
        var aiConfig = await GetEffectiveAiConfigAsync(options, cancellationToken);
        if (aiConfig == null)
        {
            yield return Error("Chưa có AI config active trong hệ thống. Vui lòng kích hoạt AI config trước khi chạy prompt.");
            yield break;
        }

        MissionSuggestionMetadata? pipelineMetadata = null;
        int? suggestionId = null;
        MissionSuggestionPipelineState? pipelineFallbackState = null;

        if (ShouldUsePipeline(options))
        {
            pipelineMetadata = CreateSuggestionMetadataForPipeline();
            suggestionId = options.PersistSuggestion
                ? await EnsureSuggestionRecordAsync(clusterId, pipelineMetadata, cancellationToken)
                : null;

            await using var pipelineEnumerator = GeneratePipelineSuggestionStreamAsync(
                sosRequests,
                nearbyDepots,
                availableNearbyTeams,
                isMultiDepotRecommended,
                clusterId,
                suggestionId,
                pipelineMetadata,
                aiConfig,
                options,
                cancellationToken).GetAsyncEnumerator(cancellationToken);

            MissionSuggestionPipelineFallbackException? pipelineFallback = null;

            while (true)
            {
                bool movedNext;
                try
                {
                    movedNext = await pipelineEnumerator.MoveNextAsync();
                }
                catch (MissionSuggestionPipelineFallbackException ex)
                {
                    pipelineFallback = ex;
                    pipelineFallbackState = ex.State;
                    break;
                }

                if (!movedNext)
                    yield break;

                yield return pipelineEnumerator.Current;
            }

            if (pipelineFallback is not null)
            {
                if (pipelineMetadata.Pipeline is not null)
                {
                    pipelineMetadata.Pipeline.PipelineStatus = "fallback";
                    pipelineMetadata.Pipeline.UsedLegacyFallback = true;
                    pipelineMetadata.Pipeline.LegacyFallbackReason = pipelineFallback.Message;
                    pipelineMetadata.Pipeline.FinalResultSource = "legacy";
                    await SaveSuggestionMetadataAsync(suggestionId, pipelineMetadata, cancellationToken);
                }

                _logger.LogWarning(pipelineFallback, "Mission suggestion pipeline fell back to legacy planning");
            }
        }

        yield return Status("Đang tải cấu hình AI agent...");

        var prompt = await GetLegacyPromptAsync(options, cancellationToken);
        if (prompt == null)
        {
            yield return Error("Chưa có prompt 'MissionPlanning' đang được kích hoạt. Vui lòng cấu hình trong quản trị hệ thống.");
            yield break;
        }

        var settings = _settingsResolver.Resolve(aiConfig);

        // Enforce minimum 32K tokens - mission plans with tool calls can be very long
        var maxTokens = Math.Max(settings.MaxTokens, 32768);

        // Build the initial user message (no pre-loaded depot data; agent fetches via tools)
        var sosDataJson = BuildSosRequestsData(sosRequests);
        var userMessage = (prompt.UserPromptTemplate ?? string.Empty)
            .Replace("{{sos_requests_data}}", sosDataJson)
            .Replace("{{total_count}}", sosRequests.Count.ToString())
            .Replace("{{depots_data}}", "(Dữ liệu kho không được truyền trực tiếp. Hãy gọi công cụ searchInventory để tra cứu vật phẩm khả dụng trong các kho hợp lệ của cluster hiện tại, sau đó chọn đúng một kho phù hợp nhất cho toàn mission.)")
            .TrimEnd();

        var nearbyTeamsNote = availableNearbyTeams.Count > 0
            ? $"\n\nDữ liệu đội cứu hộ không được truyền trực tiếp. Hãy gọi công cụ getTeams để xem {availableNearbyTeams.Count} đội nearby currently available trong bán kính cluster. Công cụ này chỉ trả về các đội gần nhất trong pool đó, không bao giờ mở rộng ra team xa hơn."
            : "\n\nHiện không có đội Available nào trong bán kính cluster. Nếu công cụ getTeams trả về rỗng, không được tự bịa team ngoài vùng; hãy để suggested_team = null và ghi rõ cần manual review.";

        userMessage += nearbyTeamsNote;

        var systemPrompt = (prompt.SystemPrompt ?? string.Empty).TrimEnd()
            + "\n\n" + BuildAgentInstructions(isMultiDepotRecommended);

        yield return Status($"AI agent ({settings.Provider}/{settings.Model}) đang phân tích {sosRequests.Count} SOS request...");

        var messages = new List<AiChatMessage>
        {
            AiChatMessage.User(userMessage)
        };

        var tools = BuildToolDefinitions();
        var providerClient = _aiProviderClientFactory.GetClient(settings.Provider);

        string? finalText = null;

        for (int turn = 0; turn < MaxAgentTurns; turn++)
        {
            if (cancellationToken.IsCancellationRequested) yield break;

            AiCompletionResponse? response = null;
            string? sendError = null;
            const int maxSendRetries = 3;
            for (int attempt = 0; attempt < maxSendRetries; attempt++)
            {
                try
                {
                    response = await providerClient.CompleteAsync(new AiCompletionRequest
                    {
                        Provider = settings.Provider,
                        Model = settings.Model,
                        ApiUrl = settings.ApiUrl,
                        ApiKey = settings.ApiKey,
                        SystemPrompt = systemPrompt,
                        Temperature = settings.Temperature,
                        MaxTokens = maxTokens,
                        Timeout = TimeSpan.FromSeconds(120),
                        Messages = messages,
                        Tools = tools
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    sendError = ex.Message;
                    break;
                }

                if (response.HttpStatusCode != 503)
                    break;

                if (attempt < maxSendRetries - 1)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt + 2)); // 4s, 8s, 16s
                    _logger.LogWarning(
                        "Provider {provider} returned 503 (turn={turn}, attempt={attempt}), retrying in {delay}s...",
                        settings.Provider, turn, attempt + 1, (int)delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }

            if (sendError != null)
            {
                yield return Error($"Lỗi kết nối tới AI: {sendError}");
                yield break;
            }

            if (response == null)
            {
                yield return Error("AI không phản hồi. Vui lòng thử lại sau.");
                yield break;
            }

            _logger.LogInformation(
                "Mission AI turn completed: Provider={provider}, Model={model}, Turn={turn}, LatencyMs={latency}, ToolCalls={toolCalls}, FinishReason={finishReason}, StatusCode={statusCode}",
                settings.Provider,
                settings.Model,
                turn + 1,
                response.LatencyMs,
                response.ToolCalls.Count,
                response.FinishReason,
                response.HttpStatusCode);

            if (response.HttpStatusCode is >= 400)
            {
                _logger.LogError(
                    "AI API error turn={turn}: Provider={provider}, Status={status}, Error={error}",
                    turn,
                    settings.Provider,
                    response.HttpStatusCode,
                    response.ErrorBody);
                yield return Error($"AI trả về lỗi ({response.HttpStatusCode}). Vui lòng thử lại sau.");
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(response.BlockReason)
                && !string.Equals(response.BlockReason, "BLOCK_REASON_UNSPECIFIED", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Provider blocked mission prompt (turn={turn}): Provider={provider}, BlockReason={reason}",
                    turn,
                    settings.Provider,
                    response.BlockReason);
                yield return Error($"Yêu cầu bị chặn bởi bộ lọc AI ({response.BlockReason}). Vui lòng thử lại hoặc điều chỉnh nội dung SOS.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(response.Text) && response.ToolCalls.Count == 0)
            {
                var finishReason = response.FinishReason ?? "(no content)";
                _logger.LogWarning(
                    "Provider returned empty content (turn={turn}), provider={provider}, finishReason={reason}. Raw snippet: {raw}",
                    turn, finishReason,
                    settings.Provider,
                    response.RawResponse?.Length > 500 ? response.RawResponse[..500] : response.RawResponse);

                // Retry once on transient failures, otherwise surface the error
                if (finishReason is "SAFETY" or "RECITATION" or "OTHER" or "BLOCKLIST" or "PROHIBITED_CONTENT" or "content_filter")
                {
                    yield return Error($"Nội dung bị lọc bởi AI ({finishReason}). Vui lòng thử lại sau.");
                    yield break;
                }
                if (turn == 0 && finishReason is "MAX_TOKENS")
                {
                    yield return Error("AI vượt giới hạn token ở lượt đầu. Vui lòng thử lại.");
                    yield break;
                }

                yield return Error($"AI không trả về nội dung (finishReason={finishReason}). Vui lòng thử lại.");
                yield break;
            }

            messages.Add(AiChatMessage.Assistant(response.Text, response.ToolCalls));

            if (response.ToolCalls.Count == 0)
            {
                // No function calls → final answer
                finalText = response.Text;
                break;
            }

            // Execute each function call
            foreach (var toolCall in response.ToolCalls)
            {
                yield return Status($"Agent đang gọi công cụ: {toolCall.Name}(...)");

                JsonElement toolResult;
                try
                {
                    toolResult = await ExecuteToolAsync(toolCall.Name, toolCall.Arguments, nearbyDepots, availableNearbyTeams, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Tool {name} threw an exception", toolCall.Name);
                    toolResult = JsonSerializer.SerializeToElement(new { error = ex.Message });
                }

                yield return Status($"Công cụ {toolCall.Name}() đã trả về kết quả.");
                messages.Add(AiChatMessage.Tool(toolCall.Id, toolCall.Name, toolResult));
            }
        }

        if (string.IsNullOrWhiteSpace(finalText))
        {
            yield return Error("AI agent không đưa ra phản hồi cuối cùng sau tối đa số vòng lặp cho phép.");
            yield break;
        }

        yield return Status("Đang xử lý kết quả...");

        _logger.LogDebug("Raw AI response (final turn):\n{raw}", finalText);

        var result       = ParseMissionSuggestion(finalText);
        result.IsSuccess     = true;
        result.ModelName     = settings.Model;
        result.RawAiResponse = finalText;
        var selection = SelectExecutableMissionResult(
            result,
            sosRequests,
            pipelineFallbackState,
            pipelineFallbackState?.DraftActivities,
            "legacy");
        result = selection.Result;
        await FinalizeSuggestionResultAsync(
            result,
            sosRequests,
            nearbyDepots,
            availableNearbyTeams,
            isMultiDepotRecommended,
            clusterId,
            suggestionId,
            pipelineMetadata,
            pipelineFallbackState?.DraftActivities,
            selection.FinalResultSource,
            options,
            cancellationToken);

        _logger.LogInformation(
            "Agent mission suggestion: Provider={provider}, Model={model}, Title={title}, Type={type}, Activities={count}, Team={team}, Confidence={conf}",
            settings.Provider, settings.Model,
            result.SuggestedMissionTitle, result.SuggestedMissionType,
            result.SuggestedActivities.Count,
            result.SuggestedTeam?.TeamName ?? "none",
            result.ConfidenceScore);

        yield return new SseMissionEvent { EventType = "result", Result = result };
    }

    // --- Tool execution --------------------------------------------------------

    private async Task<JsonElement> ExecuteToolAsync(
        string toolName,
        JsonElement args,
        IReadOnlyCollection<DepotSummary>? nearbyDepots,
        IReadOnlyCollection<AgentTeamInfo> nearbyTeams,
        CancellationToken ct)
    {
        switch (toolName)
        {
            case "searchInventory":
            {
                var category = args.TryGetProperty("category", out var c) ? c.GetString() ?? string.Empty : string.Empty;
                var type     = args.TryGetProperty("type",     out var t) ? t.GetString() : null;
                var page     = args.TryGetProperty("page",     out var p) && p.TryGetInt32(out var pv) ? pv : 1;
                var allowedDepotIds = nearbyDepots?
                    .Select(depot => depot.Id)
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                var (items, total) = await _depotInventoryRepository.SearchForAgentAsync(
                    category, type, page, AgentPageSize, allowedDepotIds, ct);

                var totalPages = (int)Math.Ceiling((double)total / AgentPageSize);
                return JsonSerializer.SerializeToElement(new
                {
                    items,
                    page,
                    total_pages = totalPages,
                    total_items = total
                }, _jsonOpts);
            }

            case "getTeams":
            {
                var ability = args.TryGetProperty("ability", out var a) ? a.GetString() : null;
                var page = args.TryGetProperty("page", out var pg) && pg.TryGetInt32(out var pgv) ? pgv : 1;

                var filteredTeams = nearbyTeams
                    .Where(team => string.IsNullOrWhiteSpace(ability)
                        || (!string.IsNullOrWhiteSpace(team.TeamType)
                            && team.TeamType.Contains(ability!, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(team => team.DistanceKm ?? double.MaxValue)
                    .ThenBy(team => team.TeamName)
                    .ThenBy(team => team.TeamId)
                    .ToList();

                var total = filteredTeams.Count;
                var teams = filteredTeams
                    .Skip((page - 1) * AgentPageSize)
                    .Take(AgentPageSize)
                    .ToList();

                var totalPages = (int)Math.Ceiling((double)total / AgentPageSize);
                return JsonSerializer.SerializeToElement(new
                {
                    teams,
                    page,
                    total_pages = totalPages,
                    total_teams = total,
                    scope = "nearby_available_teams_only"
                }, _jsonOpts);
            }

            case "getAssemblyPoints":
            {
                var page = args.TryGetProperty("page", out var pg) && pg.TryGetInt32(out var pgv) ? pgv : 1;
                var assemblyPoints = await _assemblyPointRepository.GetAllAsync(ct);
                var items = assemblyPoints
                    .Where(a => a.Status == AssemblyPointStatus.Available)
                    .OrderBy(a => a.Name)
                    .Skip((page - 1) * AgentPageSize)
                    .Take(AgentPageSize)
                    .Select(a => new AgentAssemblyPointInfo
                    {
                        AssemblyPointId = a.Id,
                        Name = a.Name,
                        Latitude = a.Location?.Latitude,
                        Longitude = a.Location?.Longitude,
                        MaxCapacity = a.MaxCapacity
                    })
                    .ToList();

                var total = assemblyPoints.Count(a => a.Status == AssemblyPointStatus.Available);
                var totalPages = (int)Math.Ceiling((double)total / AgentPageSize);
                return JsonSerializer.SerializeToElement(new
                {
                    assembly_points = items,
                    page,
                    total_pages = totalPages,
                    total_items = total
                }, _jsonOpts);
            }

            default:
                return JsonSerializer.SerializeToElement(new { error = $"Unknown tool: {toolName}" });
        }
    }

    private static List<AiToolDefinition> BuildToolDefinitions() =>
    [
        new()
        {
            Name = "searchInventory",
            Description = "Tìm kiếm vật phẩm đang khả dụng theo danh mục và loại trong các kho hợp lệ của cluster hiện tại. Trả về cả consumable lẫn reusable với item_id, tên, item_type, available_quantity, kho chứa và tọa độ vị trí kho (depot_latitude, depot_longitude). Reusable còn có good_available_count, fair_available_count, poor_available_count.",
            Parameters = ParseJson(
                """
                {
                  "type": "object",
                  "properties": {
                    "category": { "type": "string", "description": "Tên danh mục vật phẩm, ví dụ: 'Nước', 'Thực phẩm', 'Y tế', 'Quần áo'" },
                    "type": { "type": "string", "description": "Tên loại hoặc tên vật phẩm cụ thể trong danh mục (tuỳ chọn)" },
                    "page": { "type": "integer", "description": "Số trang (bắt đầu từ 1)" }
                  },
                  "required": ["category"]
                }
                """)
        },
        new()
        {
            Name = "getTeams",
            Description = "Tìm kiếm đội cứu hộ trong pool nearby teams của cluster hiện tại. Có thể lọc theo loại kỹ năng/team_type. Trả về team_id, tên, loại, trạng thái, số thành viên, vị trí điểm tập kết (assembly_point_name, latitude, longitude) và distance_km.",
            Parameters = ParseJson(
                """
                {
                  "type": "object",
                  "properties": {
                    "ability": { "type": "string", "description": "Lọc theo loại kỹ năng/team_type (tuỳ chọn)" },
                    "available": { "type": "boolean", "description": "Chỉ mang tính tương thích. Công cụ này luôn chỉ trả về nearby teams đang Available; truyền false cũng không mở rộng phạm vi." },
                    "page": { "type": "integer", "description": "Số trang (bắt đầu từ 1)" }
                  },
                  "required": []
                }
                """)
        },
        new()
        {
            Name = "getAssemblyPoints",
            Description = "Lấy danh sách điểm tập kết đang hoạt động để chọn nơi tập kết gần nhất cho activity RESCUE hoặc EVACUATE. Trả về assembly_point_id, tên, sức chứa tối đa và tọa độ.",
            Parameters = ParseJson(
                """
                {
                  "type": "object",
                  "properties": {
                    "page": { "type": "integer", "description": "Số trang (bắt đầu từ 1)" }
                  },
                  "required": []
                }
                """)
        }
    ];

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    #region AI Response Models

    private class AiMissionSuggestion
    {
        [JsonPropertyName("mission_title")]
        public string? MissionTitle { get; set; }

        [JsonPropertyName("mission_type")]
        public string? MissionType { get; set; }

        [JsonPropertyName("priority_score")]
        public double PriorityScore { get; set; }

        [JsonPropertyName("severity_level")]
        public string? SeverityLevel { get; set; }

        [JsonPropertyName("overall_assessment")]
        public string? OverallAssessment { get; set; }

        [JsonPropertyName("activities")]
        public List<AiActivity>? Activities { get; set; }

        [JsonPropertyName("resources")]
        public List<AiResource>? Resources { get; set; }

        [JsonPropertyName("suggested_team")]
        public AiSuggestedTeam? SuggestedTeam { get; set; }

        [JsonPropertyName("estimated_duration")]
        public string? EstimatedDuration { get; set; }

        [JsonPropertyName("special_notes")]
        public string? SpecialNotes { get; set; }

        [JsonPropertyName("needs_additional_depot")]
        public bool NeedsAdditionalDepot { get; set; }

        [JsonPropertyName("supply_shortages")]
        public List<AiSupplyShortage>? SupplyShortages { get; set; }

        [JsonPropertyName("confidence_score")]
        public double ConfidenceScore { get; set; }
    }

    private class AiSupplyShortage
    {
        [JsonPropertyName("sos_request_id")]
        public int? SosRequestId { get; set; }

        [JsonPropertyName("item_id")]
        public int? ItemId { get; set; }

        [JsonPropertyName("item_name")]
        public string? ItemName { get; set; }

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        [JsonPropertyName("selected_depot_id")]
        public int? SelectedDepotId { get; set; }

        [JsonPropertyName("selected_depot_name")]
        public string? SelectedDepotName { get; set; }

        [JsonPropertyName("needed_quantity")]
        public int NeededQuantity { get; set; }

        [JsonPropertyName("available_quantity")]
        public int AvailableQuantity { get; set; }

        [JsonPropertyName("missing_quantity")]
        public int MissingQuantity { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    private class AiSuggestedTeam
    {
        [JsonPropertyName("team_id")]
        public int TeamId { get; set; }

        [JsonPropertyName("team_name")]
        public string? TeamName { get; set; }

        [JsonPropertyName("team_type")]
        public string? TeamType { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("assembly_point_id")]
        public int? AssemblyPointId { get; set; }

        [JsonPropertyName("assembly_point_name")]
        public string? AssemblyPointName { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("distance_km")]
        public double? DistanceKm { get; set; }
    }

    private class AiSupplyToCollect
    {
        [JsonPropertyName("item_id")]
        public int? ItemId { get; set; }

        [JsonPropertyName("item_name")]
        public string? ItemName { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }
    }

    private class AiActivity
    {
        [JsonPropertyName("step")]
        public int Step { get; set; }

        [JsonPropertyName("activity_type")]
        public string? ActivityType { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("priority")]
        public string? Priority { get; set; }

        [JsonPropertyName("estimated_time")]
        public string? EstimatedTime { get; set; }

        [JsonPropertyName("execution_mode")]
        public string? ExecutionMode { get; set; }

        [JsonPropertyName("required_team_count")]
        public int? RequiredTeamCount { get; set; }

        [JsonPropertyName("coordination_group_key")]
        public string? CoordinationGroupKey { get; set; }

        [JsonPropertyName("coordination_notes")]
        public string? CoordinationNotes { get; set; }

        [JsonPropertyName("sos_request_id")]
        public int? SosRequestId { get; set; }

        [JsonPropertyName("depot_id")]
        public int? DepotId { get; set; }

        [JsonPropertyName("depot_name")]
        public string? DepotName { get; set; }

        [JsonPropertyName("depot_address")]
        public string? DepotAddress { get; set; }

        [JsonPropertyName("depot_latitude")]
        public double? DepotLatitude { get; set; }

        [JsonPropertyName("depot_longitude")]
        public double? DepotLongitude { get; set; }

        [JsonPropertyName("assembly_point_id")]
        public int? AssemblyPointId { get; set; }

        [JsonPropertyName("assembly_point_name")]
        public string? AssemblyPointName { get; set; }

        [JsonPropertyName("assembly_point_latitude")]
        public double? AssemblyPointLatitude { get; set; }

        [JsonPropertyName("assembly_point_longitude")]
        public double? AssemblyPointLongitude { get; set; }

        [JsonPropertyName("supplies_to_collect")]
        public List<AiSupplyToCollect>? SuppliesToCollect { get; set; }

        [JsonPropertyName("suggested_team")]
        public AiSuggestedTeam? SuggestedTeam { get; set; }
    }

    private class AiResource
    {
        [JsonPropertyName("resource_type")]
        public string? ResourceType { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("quantity")]
        public int? Quantity { get; set; }

        [JsonPropertyName("priority")]
        public string? Priority { get; set; }
    }

    #endregion
}

