using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<RescueMissionSuggestionService> _logger;

    private const int AgentPageSize = 10;
    private const string CollectSuppliesActivityType = "COLLECT_SUPPLIES";
    private const string ReturnSuppliesActivityType = "RETURN_SUPPLIES";
    private const string ReturnAssemblyPointActivityType = "RETURN_ASSEMBLY_POINT";
    private const string ReusableItemType = "Reusable";
    private const string SingleTeamExecutionMode = "SingleTeam";
    private const string DefaultReturnAssemblyEstimatedTime = "20 phút";
    private const string DefaultInventoryBackedCollectEstimatedTime = "30 phút";
    private const string TransportationInventoryCategory = "transportation";
    private const string RescueInventoryCategory = "rescue";

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
            && (!RequiresMixedBranches || (HasRescueBranch && HasReliefBranch))
            && string.IsNullOrWhiteSpace(FailureReason);
    }

    private sealed record MissionSosRouteConstraint(
        int SosRequestId,
        bool IsRescueLike,
        bool NeedsImmediateSafeTransfer,
        bool? CanWaitForCombinedMission,
        bool RequiresSupplyBeforeRescue);

    private sealed class InventoryBackedResourceNeed
    {
        public string ResourceType { get; init; } = string.Empty;
        public string CategoryKeyword { get; init; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public string? Description { get; init; }
        public string? Priority { get; init; }
        public SuggestedResourceDto? SourceResource { get; init; }
        public List<string> SearchTypes { get; init; } = [];
        public List<int> RelatedSosIds { get; init; } = [];
    }

    private sealed record OperationalTransportSignals(
        bool RequiresWaterTransport,
        bool RequiresEvacuationTransport,
        bool RequiresRescueEquipment);

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
                    if (evt.Result != null)
                    {
                        evt.Result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                        if (!options.PersistSuggestion)
                            evt.Result.SuggestionId = null;

                        return evt.Result;
                    }

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
            - Nếu SOS nhắc ngập sâu, cô lập, mắc kẹt, chia cắt hoặc sơ tán, mặc định phải kiểm tra tồn kho phương tiện/thiet bị cứu hộ trước khi chốt kế hoạch.
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
            - Không tạo `warnings[]`, không tạo warning code riêng, không chèn warning schema mới vào JSON.
            - Cảnh báo mixed mission không phải là lý do để bỏ trống `activities`. Khi đã trả mission JSON, `activities` phải là execution plan cụ thể.
            - Nếu cluster mixed có SOS rescue khẩn cấp cần đưa về nơi an toàn ngay, hãy giữ route an toàn nhất có thể và cảnh báo coordinator thật rõ.
            - Nếu SOS rescue không cần cứu gấp và có thể chờ mission kết hợp, có thể xếp route `COLLECT_SUPPLIES -> DELIVER_SUPPLIES` trước rồi mới chuyển sang rescue branch.
            - Nếu SOS rescue khẩn cấp, ưu tiên xử lý nhánh rescue trước phần việc không liên quan. Có thể `COLLECT_SUPPLIES` trước rescue nếu route thực tế cần lấy vật phẩm hoặc thiết bị từ kho để triển khai ngoài hiện trường.
            - Nếu route mixed hiện tại không an toàn, hãy rewrite lại thứ tự hoạt động. Không được thay route bằng `activities = []`.

            ## HỢP ĐỒNG CẢNH BÁO NỘI BỘ
            - Bạn phải đọc toàn bộ cluster SOS, đặc biệt `raw_message`, `structured_data`, `incident_notes`, `target_victims`, `ai_analysis`, để tự đánh giá cảnh báo tổng thể.
            - Luôn trả thêm 5 field top-level sau:
              - `warning_level`: `none|light|medium|strong`
              - `warning_title`
              - `warning_message`
              - `warning_related_sos_ids`
              - `warning_reason`
            - `warning_level = light` khi chỉ là rủi ro theo dõi hoặc lưu ý nhẹ, plan vẫn thực thi được bình thường.
            - `warning_level = medium` khi coordinator nên xem xét thủ công trước khi chốt.
            - `warning_level = strong` khi có nhiều SOS critical/urgent, mixed rescue-relief có rủi ro an toàn, hoặc thiếu dữ liệu/đội/vật tư quan trọng cần coordinator can thiệp.
            - `warning_message` phải nói rõ SOS nào là nguồn rủi ro chính và coordinator cần chú ý điều gì.
            - Nếu không có warning đáng kể thì trả `warning_level = "none"` và các field warning còn lại là null hoặc `[]`.

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
        var result = new RescueMissionSuggestionResult
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
            SupplyShortages = parsed.SupplyShortages?.Select(MapSupplyShortage).ToList() ?? []
        };

        ApplyPipelineWarning(
            result,
            parsed.WarningLevel,
            parsed.WarningTitle,
            parsed.WarningMessage,
            parsed.WarningRelatedSosIds,
            parsed.WarningReason);

        return result;
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

        ApplyPipelineWarning(
            result,
            GetOptionalString(root, "warning_level"),
            GetOptionalString(root, "warning_title"),
            GetOptionalString(root, "warning_message"),
            ParseWarningRelatedSosIds(root),
            GetOptionalString(root, "warning_reason"));

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

    private static void ApplyPipelineWarning(
        RescueMissionSuggestionResult result,
        string? warningLevel,
        string? warningTitle,
        string? warningMessage,
        IEnumerable<int>? warningRelatedSosIds,
        string? warningReason)
    {
        var normalizedLevel = NormalizePipelineWarningLevel(warningLevel);
        if (string.IsNullOrWhiteSpace(normalizedLevel) || string.Equals(normalizedLevel, "none", StringComparison.Ordinal))
            return;

        var warningText = BuildPipelineWarningText(warningTitle, warningMessage, warningRelatedSosIds, warningReason);
        if (string.IsNullOrWhiteSpace(warningText))
            return;

        if (normalizedLevel is "medium" or "strong")
            result.NeedsManualReview = true;

        if (normalizedLevel == "strong" && LooksLikeMixedRouteWarning(warningText))
        {
            result.MixedRescueReliefWarning = warningText;
            return;
        }

        result.SpecialNotes = AppendSpecialNote(result.SpecialNotes, warningText);
    }

    private static string? NormalizePipelineWarningLevel(string? warningLevel)
    {
        return warningLevel?.Trim().ToLowerInvariant() switch
        {
            "light" => "light",
            "medium" => "medium",
            "strong" => "strong",
            "none" => "none",
            _ => null
        };
    }

    private static string? BuildPipelineWarningText(
        string? warningTitle,
        string? warningMessage,
        IEnumerable<int>? warningRelatedSosIds,
        string? warningReason)
    {
        var relatedIds = warningRelatedSosIds?
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToList() ?? [];

        var relatedSosNote = relatedIds.Count == 0
            ? null
            : $"SOS liên quan: {string.Join(", ", relatedIds.Select(id => $"#{id}"))}.";
        var reasonNote = string.IsNullOrWhiteSpace(warningReason)
            ? null
            : $"Lý do: {warningReason.Trim()}";

        return JoinNotes(warningTitle, warningMessage, reasonNote, relatedSosNote);
    }

    private static bool LooksLikeMixedRouteWarning(string warningText)
    {
        var normalized = SosPriorityRuleConfigSupport.NormalizeKey(warningText);
        if (normalized.Contains("MIXED", StringComparison.Ordinal))
            return true;

        var hasRescueSignal =
            normalized.Contains("RESCUE", StringComparison.Ordinal)
            || normalized.Contains("CUU_HO", StringComparison.Ordinal)
            || normalized.Contains("CAP_CUU", StringComparison.Ordinal)
            || normalized.Contains("EVACUATE", StringComparison.Ordinal);
        var hasReliefSignal =
            normalized.Contains("RELIEF", StringComparison.Ordinal)
            || normalized.Contains("CUU_TRO", StringComparison.Ordinal)
            || normalized.Contains("CAP_PHAT", StringComparison.Ordinal)
            || normalized.Contains("DELIVER_SUPPLIES", StringComparison.Ordinal);

        return hasRescueSignal && hasReliefSignal;
    }

    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }

    private static List<int> ParseWarningRelatedSosIds(JsonElement root)
    {
        if (!root.TryGetProperty("warning_related_sos_ids", out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray()
                .Select(element =>
                {
                    if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numberValue))
                        return numberValue;

                    return element.ValueKind == JsonValueKind.String
                        && int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stringValue)
                            ? stringValue
                            : (int?)null;
                })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return Regex.Matches(value.GetString() ?? string.Empty, @"\d+")
                .Select(match => int.Parse(match.Value, CultureInfo.InvariantCulture))
                .ToList();
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var singleValue)
            ? [singleValue]
            : [];
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
        static List<int> ExtractIntArray(string src, string field)
        {
            var arrayMatch = Regex.Match(src, $@"""{field}""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
            if (!arrayMatch.Success)
                return [];

            return Regex.Matches(arrayMatch.Groups[1].Value, @"\d+")
                .Select(match => int.Parse(match.Value, CultureInfo.InvariantCulture))
                .ToList();
        }

        var result = new RescueMissionSuggestionResult
        {
            SuggestedMissionTitle = ExtractStr(text, "mission_title") ?? "Nhiệm vụ giải cứu",
            SuggestedMissionType = ExtractStr(text, "mission_type"),
            SuggestedPriorityScore = ExtractNum(text, "priority_score"),
            SuggestedSeverityLevel = ExtractStr(text, "severity_level"),
            OverallAssessment = ExtractStr(text, "overall_assessment")?.Replace("\n", " ").Replace("\r", " ").Trim(),
            EstimatedDuration = ExtractStr(text, "estimated_duration"),
            SpecialNotes = ExtractStr(text, "special_notes"),
            NeedsAdditionalDepot = ExtractBool(text, "needs_additional_depot")
        };

        ApplyPipelineWarning(
            result,
            ExtractStr(text, "warning_level"),
            ExtractStr(text, "warning_title"),
            ExtractStr(text, "warning_message"),
            ExtractIntArray(text, "warning_related_sos_ids"),
            ExtractStr(text, "warning_reason"));

        return result;
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
        IReadOnlyCollection<SuggestedActivityDto>? expectedActivities = null,
        MissionRequirementsFragment? requirements = null)
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

        BackfillMissingSupplyRouteDetailsFromExpectedActivities(result.SuggestedActivities, expectedActivities);

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
        else
        {
            failureReason = AssessMissionActivityRoute(executableActivities, sosRequests, requirements);
        }

        return new MissionExecutionAssessment(
            HasExecutableActivities: hasExecutableActivities,
            RequiresMixedBranches: requiresMixedBranches,
            HasRescueBranch: hasRescueBranch,
            HasReliefBranch: hasReliefBranch,
            FailureReason: failureReason);
    }

    private static void BackfillMissingSupplyRouteDetailsFromExpectedActivities(
        List<SuggestedActivityDto> activities,
        IReadOnlyCollection<SuggestedActivityDto>? expectedActivities)
    {
        if (activities.Count == 0 || expectedActivities is not { Count: > 0 })
            return;

        var expectedSupplyActivities = expectedActivities
            .Where(IsSupplyDependencyActivity)
            .Where(activity => activity.DepotId.HasValue || activity.SuppliesToCollect is { Count: > 0 })
            .OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue)
            .ToList();

        if (expectedSupplyActivities.Count == 0)
            return;

        foreach (var activity in activities
                     .Where(IsSupplyDependencyActivity)
                     .OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue))
        {
            var needsDepot = !activity.DepotId.HasValue;
            var needsSupplies = activity.SuppliesToCollect is not { Count: > 0 };
            if (!needsDepot && !needsSupplies)
                continue;

            var referenceActivity = FindExpectedSupplyActivityForBackfill(activity, expectedSupplyActivities);
            if (referenceActivity is null)
                continue;

            activity.DepotId ??= referenceActivity.DepotId;
            activity.DepotName ??= referenceActivity.DepotName;
            activity.DepotAddress ??= referenceActivity.DepotAddress;

            if (needsSupplies && referenceActivity.SuppliesToCollect is { Count: > 0 })
                activity.SuppliesToCollect = referenceActivity.SuppliesToCollect.Select(CloneSupply).ToList();
        }
    }

    private static bool IsSupplyDependencyActivity(SuggestedActivityDto activity) =>
        IsCollectActivity(activity)
        || string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase);

    private static SuggestedActivityDto? FindExpectedSupplyActivityForBackfill(
        SuggestedActivityDto activity,
        IReadOnlyCollection<SuggestedActivityDto> expectedActivities)
    {
        var targetStep = activity.Step > 0 ? activity.Step : (int?)null;
        var targetRouteKey = BuildSupplyRouteKey(activity);
        var targetPrimarySosId = GetPrimarySosId(activity);
        var targetSosIds = GetReferencedSosIds(activity);

        return expectedActivities
            .Where(candidate => string.Equals(candidate.ActivityType, activity.ActivityType, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = ScoreExpectedSupplyActivityForBackfill(
                    candidate,
                    activity,
                    targetRouteKey,
                    targetStep,
                    targetPrimarySosId,
                    targetSosIds),
                StepDistance = targetStep.HasValue && candidate.Step > 0
                    ? Math.Abs(candidate.Step - targetStep.Value)
                    : int.MaxValue
            })
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.StepDistance)
            .FirstOrDefault()
            ?.Candidate;
    }

    private static int ScoreExpectedSupplyActivityForBackfill(
        SuggestedActivityDto candidate,
        SuggestedActivityDto activity,
        string targetRouteKey,
        int? targetStep,
        int? targetPrimarySosId,
        IReadOnlySet<int> targetSosIds)
    {
        var score = 0;

        if (targetStep.HasValue && candidate.Step == targetStep.Value)
            score += 200;

        var candidateRouteKey = BuildSupplyRouteKey(candidate);
        if (string.Equals(candidateRouteKey, targetRouteKey, StringComparison.OrdinalIgnoreCase))
            score += 80;

        if (activity.DepotId.HasValue)
        {
            if (candidate.DepotId == activity.DepotId)
                score += 60;
            else if (candidate.DepotId.HasValue)
                score -= 40;
        }
        else if (candidate.DepotId.HasValue)
        {
            score += 10;
        }

        if (targetPrimarySosId.HasValue && GetPrimarySosId(candidate) == targetPrimarySosId.Value)
            score += 40;

        if (targetSosIds.Count > 0)
        {
            var candidateSosIds = GetReferencedSosIds(candidate);
            if (candidateSosIds.SetEquals(targetSosIds))
                score += 30;
            else if (candidateSosIds.Overlaps(targetSosIds))
                score += 20;
        }

        if (candidate.SuppliesToCollect is { Count: > 0 })
            score += 10;

        return score;
    }

    private static string? AssessMissionActivityRoute(
        IReadOnlyList<SuggestedActivityDto> activities,
        IReadOnlyCollection<SosRequestSummary> sosRequests,
        MissionRequirementsFragment? requirements)
    {
        var routeConstraints = BuildMissionSosRouteConstraints(sosRequests, requirements);
        var supplyFailure = AssessSupplyRouteDependencies(activities, routeConstraints);
        if (!string.IsNullOrWhiteSpace(supplyFailure))
            return supplyFailure;

        return AssessMixedMissionSafety(activities, routeConstraints);
    }

    private static string? AssessSupplyRouteDependencies(
        IReadOnlyList<SuggestedActivityDto> activities,
        IReadOnlyDictionary<int, MissionSosRouteConstraint> routeConstraints)
    {
        var ledgers = new Dictionary<(int DepotId, string RouteKey), Dictionary<string, int>>();
        var collectActivities = new List<(SuggestedActivityDto Activity, string RouteKey)>();

        foreach (var activity in activities.OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue))
        {
            if (IsReturnAssemblyPointActivity(activity) || IsReturnActivity(activity))
                continue;

            if (!IsCollectActivity(activity)
                && !string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!activity.DepotId.HasValue)
                return $"Activity step {activity.Step} ({activity.ActivityType}) is missing depot_id.";

            if (activity.SuppliesToCollect is not { Count: > 0 })
                return $"Activity step {activity.Step} ({activity.ActivityType}) must include supplies_to_collect.";

            var routeKey = BuildSupplyRouteKey(activity);
            var ledgerKey = (activity.DepotId.Value, routeKey);
            if (!ledgers.TryGetValue(ledgerKey, out var routeLedger))
            {
                routeLedger = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                ledgers[ledgerKey] = routeLedger;
            }

            if (IsCollectActivity(activity))
            {
                collectActivities.Add((activity, routeKey));

                foreach (var supply in activity.SuppliesToCollect.Where(supply => supply.Quantity > 0))
                {
                    var supplyKey = BuildSupplyLedgerKey(supply.ItemId, supply.ItemName);
                    routeLedger.TryGetValue(supplyKey, out var quantity);
                    routeLedger[supplyKey] = quantity + supply.Quantity;
                }

                continue;
            }

            foreach (var supply in activity.SuppliesToCollect.Where(supply => supply.Quantity > 0))
            {
                var supplyKey = BuildSupplyLedgerKey(supply.ItemId, supply.ItemName);
                routeLedger.TryGetValue(supplyKey, out var availableQuantity);
                if (availableQuantity < supply.Quantity)
                {
                    return
                        $"Activity step {activity.Step} (DELIVER_SUPPLIES) exceeds collected quantity or appears before COLLECT_SUPPLIES for '{supply.ItemName}'.";
                }

                routeLedger[supplyKey] = availableQuantity - supply.Quantity;
            }
        }

        foreach (var (collectActivity, routeKey) in collectActivities)
        {
            if (CollectActivityHasFollowUpUsage(activities, collectActivity, routeKey, routeConstraints))
                continue;

            return
                $"Activity step {collectActivity.Step} (COLLECT_SUPPLIES) does not feed any later delivery or rescue flow in the same route.";
        }

        return null;
    }

    private static string? AssessMixedMissionSafety(
        IReadOnlyList<SuggestedActivityDto> activities,
        IReadOnlyDictionary<int, MissionSosRouteConstraint> routeConstraints)
    {
        if (routeConstraints.Count == 0)
            return null;

        foreach (var constraint in routeConstraints.Values.Where(constraint => constraint.IsRescueLike))
        {
            var referencedActivities = activities
                .Select((activity, index) => new { Activity = activity, Index = index })
                .Where(entry => ReferencesSos(entry.Activity, constraint.SosRequestId))
                .ToList();

            if (referencedActivities.Count == 0)
                continue;

            var firstRescueIndex = referencedActivities
                .Where(entry => IsSafetyCriticalActivity(entry.Activity.ActivityType))
                .Select(entry => (int?)entry.Index)
                .FirstOrDefault();

            if (constraint.NeedsImmediateSafeTransfer && firstRescueIndex is null)
            {
                return $"Urgent SOS #{constraint.SosRequestId} is missing rescue/medical/evacuation activities.";
            }

            if (firstRescueIndex is null)
                continue;

            if (constraint.NeedsImmediateSafeTransfer)
            {
                var preRescueFailure = AssessPreRescueOrdering(
                    activities,
                    routeConstraints,
                    constraint,
                    firstRescueIndex.Value);
                if (!string.IsNullOrWhiteSpace(preRescueFailure))
                    return preRescueFailure;
            }

        }

        return null;
    }

    private static string? AssessPreRescueOrdering(
        IReadOnlyList<SuggestedActivityDto> activities,
        IReadOnlyDictionary<int, MissionSosRouteConstraint> routeConstraints,
        MissionSosRouteConstraint targetConstraint,
        int firstRescueIndex)
    {
        for (var index = 0; index < firstRescueIndex; index++)
        {
            var activity = activities[index];
            var referencedSosIds = GetReferencedSosIds(activity);
            if (referencedSosIds.Count == 0)
                continue;

            if (referencedSosIds.Contains(targetConstraint.SosRequestId))
            {
                if (!string.Equals(activity.ActivityType, CollectSuppliesActivityType, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase))
                {
                    return
                        $"Urgent SOS #{targetConstraint.SosRequestId} cannot start with '{activity.ActivityType}' before rescue begins.";
                }

                continue;
            }

            var referencedUrgentRescue = referencedSosIds.Any(
                sosId => routeConstraints.TryGetValue(sosId, out var constraint)
                    && constraint.IsRescueLike
                    && constraint.NeedsImmediateSafeTransfer);

            if (referencedUrgentRescue && IsAllowedUrgentPreRescueActivity(activity, routeConstraints))
                continue;

            return
                $"Urgent SOS #{targetConstraint.SosRequestId} is delayed by step {activity.Step} ({activity.ActivityType}) before rescue starts.";
        }

        return null;
    }

    private static bool IsAllowedUrgentPreRescueActivity(
        SuggestedActivityDto activity,
        IReadOnlyDictionary<int, MissionSosRouteConstraint> routeConstraints)
    {
        var referencedSosIds = GetReferencedSosIds(activity);
        if (referencedSosIds.Count == 0)
            return false;

        if (!IsSafetyCriticalActivity(activity.ActivityType)
            && !string.Equals(activity.ActivityType, CollectSuppliesActivityType, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var sosId in referencedSosIds)
        {
            if (!routeConstraints.TryGetValue(sosId, out var constraint)
                || !constraint.IsRescueLike
                || !constraint.NeedsImmediateSafeTransfer)
            {
                return false;
            }

        }

        return true;
    }

    private static bool CollectActivityHasFollowUpUsage(
        IReadOnlyList<SuggestedActivityDto> activities,
        SuggestedActivityDto collectActivity,
        string routeKey,
        IReadOnlyDictionary<int, MissionSosRouteConstraint> routeConstraints)
    {
        var collectIndex = activities
            .Select((activity, index) => new { Activity = activity, Index = index })
            .FirstOrDefault(entry => ReferenceEquals(entry.Activity, collectActivity))
            ?.Index;

        if (!collectIndex.HasValue)
            return false;

        var collectSosIds = GetReferencedSosIds(collectActivity);
        var hasCollectSosIds = collectSosIds.Count > 0;

        for (var index = collectIndex.Value + 1; index < activities.Count; index++)
        {
            var activity = activities[index];
            if (!string.Equals(BuildSupplyRouteKey(activity), routeKey, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase))
                return true;

            if (IsReturnActivity(activity))
                return true;

            if (!IsSafetyCriticalActivity(activity.ActivityType))
                continue;

            if (!hasCollectSosIds)
                return true;

            var referencedSosIds = GetReferencedSosIds(activity);
            foreach (var sosId in referencedSosIds)
            {
                if (!collectSosIds.Contains(sosId))
                    continue;

                if (!routeConstraints.TryGetValue(sosId, out var constraint))
                    continue;

                if (constraint.NeedsImmediateSafeTransfer || constraint.RequiresSupplyBeforeRescue)
                    return true;
            }
        }

        return false;
    }

    private static Dictionary<int, MissionSosRouteConstraint> BuildMissionSosRouteConstraints(
        IReadOnlyCollection<SosRequestSummary> sosRequests,
        MissionRequirementsFragment? requirements)
    {
        var requirementLookup = requirements?.SosRequirements
            .GroupBy(requirement => requirement.SosRequestId)
            .ToDictionary(group => group.Key, group => group.First()) ?? [];

        return sosRequests.ToDictionary(
            sos => sos.Id,
            sos =>
            {
                requirementLookup.TryGetValue(sos.Id, out var requirement);
                var isRescueLike = SosRequestAiAnalysisHelper.IsRescueLikeRequestType(sos.SosType);
                var needsImmediateSafeTransfer =
                    requirement?.UrgentRescueRequiresImmediateSafeTransfer
                    ?? requirement?.NeedsImmediateSafeTransfer
                    ?? sos.AiAnalysis?.NeedsImmediateSafeTransfer
                    ?? false;
                var canWait = requirement?.CanWaitForCombinedMission ?? sos.AiAnalysis?.CanWaitForCombinedMission;
                var requiresSupplyBeforeRescue =
                    requirement?.RequiresSupplyBeforeRescue
                    ?? InferRequiresSupplyBeforeRescue(requirement);

                return new MissionSosRouteConstraint(
                    sos.Id,
                    isRescueLike,
                    needsImmediateSafeTransfer,
                    canWait,
                    requiresSupplyBeforeRescue);
            });
    }

    private static bool InferRequiresSupplyBeforeRescue(MissionSosRequirementFragment? requirement)
    {
        if (requirement?.RequiredSupplies is not { Count: > 0 })
            return false;

        foreach (var supply in requirement.RequiredSupplies)
        {
            var normalizedCategory = NormalizeItemName(supply.Category ?? string.Empty);
            var normalizedName = NormalizeItemName(supply.ItemName ?? string.Empty);
            if (normalizedCategory.Contains("vehicle", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("rescue", StringComparison.OrdinalIgnoreCase)
                || normalizedCategory.Contains("equipment", StringComparison.OrdinalIgnoreCase)
                || normalizedName.Contains("xuong", StringComparison.OrdinalIgnoreCase)
                || normalizedName.Contains("cano", StringComparison.OrdinalIgnoreCase)
                || normalizedName.Contains("day", StringComparison.OrdinalIgnoreCase)
                || normalizedName.Contains("phao", StringComparison.OrdinalIgnoreCase)
                || normalizedName.Contains("cang", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSafetyCriticalActivity(string? activityType) =>
        string.Equals(activityType, "RESCUE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(activityType, "MEDICAL_AID", StringComparison.OrdinalIgnoreCase)
        || string.Equals(activityType, "EVACUATE", StringComparison.OrdinalIgnoreCase);

    private static bool ReferencesSos(SuggestedActivityDto activity, int sosRequestId) =>
        GetReferencedSosIds(activity).Contains(sosRequestId);

    private static string BuildSupplyRouteKey(SuggestedActivityDto activity)
    {
        if (activity.SuggestedTeam?.TeamId is > 0)
            return $"team:{activity.SuggestedTeam.TeamId}";

        if (!string.IsNullOrWhiteSpace(activity.CoordinationGroupKey))
            return $"coord:{activity.CoordinationGroupKey.Trim()}";

        return "mission";
    }

    private static string BuildSupplyLedgerKey(int? itemId, string? itemName)
    {
        if (itemId.HasValue && itemId.Value > 0)
            return $"item:{itemId.Value}";

        var normalizedName = NormalizeItemName(itemName);
        return string.IsNullOrWhiteSpace(normalizedName)
            ? "item:unknown"
            : $"name:{normalizedName}";
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

    private static int GetOnSitePriority(SuggestedActivityDto activity) =>
        (activity.ActivityType ?? string.Empty).ToUpperInvariant() switch
        {
            "DELIVER_SUPPLIES" => 1,
            "RESCUE" => 2,
            "MEDICAL_AID" => 3,
            "EVACUATE" => 4,
            _ => 99
        };

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
                ReferencedSosIds = GetReferencedSosIds(activity)
            })
            .ToList();

        var collectActivities = indexed
            .Where(x => IsCollectActivity(x.Activity))
            .OrderBy(x => x.OriginalIndex)
            .ToList();

        var onSiteActivities = indexed
            .Where(x => !IsCollectActivity(x.Activity) && IsOnSiteActivity(x.Activity))
            .OrderBy(x => x.OriginalIndex)
            .ToList();

        var otherActivities = indexed
            .Where(x => !IsCollectActivity(x.Activity) && !IsOnSiteActivity(x.Activity))
            .OrderBy(x => x.OriginalIndex)
            .ToList();

        var sosOrder = onSiteActivities
            .Select(x => x.PrimarySosId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var normalized = new List<SuggestedActivityDto>(activities.Count);
        var usedCollectIndexes = new HashSet<int>();

        foreach (var sosId in sosOrder)
        {
            foreach (var collect in collectActivities)
            {
                if (usedCollectIndexes.Contains(collect.OriginalIndex))
                    continue;

                if (collect.ReferencedSosIds.Contains(sosId))
                {
                    normalized.Add(collect.Activity);
                    usedCollectIndexes.Add(collect.OriginalIndex);
                }
            }

            normalized.AddRange(onSiteActivities
                .Where(x => x.PrimarySosId == sosId)
                .OrderBy(x => GetOnSitePriority(x.Activity))
                .ThenBy(x => x.OriginalIndex)
                .Select(x => x.Activity));
        }

        var leadingCollects = collectActivities
            .Where(x => !usedCollectIndexes.Contains(x.OriginalIndex))
            .Select(x => x.Activity)
            .ToList();

        normalized.InsertRange(0, leadingCollects);

        normalized.AddRange(onSiteActivities
            .Where(x => !x.PrimarySosId.HasValue)
            .Select(x => x.Activity));

        normalized.AddRange(otherActivities.Select(x => x.Activity));

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

    private async Task EnsureInventoryBackedTransportSuppliesAsync(
        RescueMissionSuggestionResult result,
        List<SosRequestSummary> sosRequests,
        List<DepotSummary> nearbyDepots,
        CancellationToken cancellationToken)
    {
        if (result.SuggestedResources.Count == 0 && result.SuggestedActivities.Count == 0)
            return;

        var needs = BuildInventoryBackedResourceNeeds(result, sosRequests);
        if (needs.Count == 0)
            return;

        var preferredDepotIds = ResolvePreferredDepotIds(result, nearbyDepots);
        if (preferredDepotIds.Count == 0)
            return;

        var depotPreferenceOrder = preferredDepotIds
            .Select((depotId, index) => new { depotId, index })
            .ToDictionary(entry => entry.depotId, entry => entry.index);

        foreach (var need in needs)
        {
            var matchedItem = await FindInventoryBackedResourceMatchAsync(
                need,
                preferredDepotIds,
                depotPreferenceOrder,
                cancellationToken);

            if (matchedItem is null)
                continue;

            var quantityToCollect = Math.Min(Math.Max(need.Quantity, 1), matchedItem.AvailableQuantity);
            if (quantityToCollect <= 0)
                continue;

            var collectActivity = FindCollectActivityForInventoryBackedResource(
                result.SuggestedActivities,
                matchedItem.DepotId,
                need.RelatedSosIds);

            if (collectActivity is null)
            {
                collectActivity = CreateInventoryBackedCollectActivity(matchedItem, need, quantityToCollect);
                result.SuggestedActivities.Add(collectActivity);
            }
            else
            {
                ApplyInventoryBackedCollectDefaults(collectActivity, matchedItem, need);
                AddOrUpdateInventoryBackedSupply(collectActivity, matchedItem, quantityToCollect);
                collectActivity.Description = AppendInventoryBackedCollectDescription(
                    collectActivity.Description,
                    matchedItem,
                    quantityToCollect,
                    need.RelatedSosIds);
            }

            TrimInventoryBackedSuggestedResource(result.SuggestedResources, need.SourceResource, quantityToCollect);
        }
    }

    private static List<InventoryBackedResourceNeed> BuildInventoryBackedResourceNeeds(
        RescueMissionSuggestionResult result,
        IReadOnlyCollection<SosRequestSummary> sosRequests)
    {
        var relatedSosIds = ResolveInventoryBackedResourceSosIds(result.SuggestedActivities, sosRequests);
        var signals = InferOperationalTransportSignals(sosRequests, result.SuggestedActivities);

        var needs = result.SuggestedResources
            .Where(resource => IsInventoryBackedTransportResource(resource))
            .Select(resource => CreateInventoryBackedResourceNeed(resource, relatedSosIds))
            .Where(need => need is not null)
            .Select(need => need!)
            .ToList();

        if (signals.RequiresWaterTransport
            && !needs.Any(need => string.Equals(need.ResourceType, "BOAT", StringComparison.OrdinalIgnoreCase)))
        {
            needs.Add(new InventoryBackedResourceNeed
            {
                ResourceType = "BOAT",
                CategoryKeyword = TransportationInventoryCategory,
                Quantity = 1,
                Description = "Water transport support",
                Priority = SelectTransportNeedPriority(result.SuggestedActivities),
                SearchTypes = BuildSearchTypeList("ca no cuu ho", "ca no", "cano", "xuong", "boat", "thuyen"),
                RelatedSosIds = relatedSosIds
            });
        }
        else if (signals.RequiresEvacuationTransport
            && !needs.Any(need => string.Equals(need.ResourceType, "VEHICLE", StringComparison.OrdinalIgnoreCase)))
        {
            needs.Add(new InventoryBackedResourceNeed
            {
                ResourceType = "VEHICLE",
                CategoryKeyword = TransportationInventoryCategory,
                Quantity = 1,
                Description = "Evacuation transport",
                Priority = SelectTransportNeedPriority(result.SuggestedActivities),
                SearchTypes = BuildSearchTypeList("xe", "phuong tien", "xe cuu thuong", "xe khach"),
                RelatedSosIds = relatedSosIds
            });
        }

        if (signals.RequiresRescueEquipment
            && !needs.Any(need => string.Equals(need.ResourceType, "EQUIPMENT", StringComparison.OrdinalIgnoreCase)))
        {
            needs.Add(new InventoryBackedResourceNeed
            {
                ResourceType = "EQUIPMENT",
                CategoryKeyword = RescueInventoryCategory,
                Quantity = 1,
                Description = "Rescue equipment",
                Priority = SelectTransportNeedPriority(result.SuggestedActivities),
                SearchTypes = BuildSearchTypeList("phao", "day", "cang", "ao phao", "cuu ho"),
                RelatedSosIds = relatedSosIds
            });
        }

        return needs;
    }

    private static InventoryBackedResourceNeed? CreateInventoryBackedResourceNeed(
        SuggestedResourceDto resource,
        List<int> relatedSosIds)
    {
        var normalizedType = (resource.ResourceType ?? string.Empty).Trim().ToUpperInvariant();
        return normalizedType switch
        {
            "BOAT" => new InventoryBackedResourceNeed
            {
                ResourceType = normalizedType,
                CategoryKeyword = TransportationInventoryCategory,
                Quantity = Math.Max(resource.Quantity ?? 1, 1),
                Description = resource.Description,
                Priority = resource.Priority,
                SourceResource = resource,
                SearchTypes = BuildSearchTypeList(resource.Description, "ca no cuu ho", "ca no", "cano", "xuong", "boat", "thuyen"),
                RelatedSosIds = relatedSosIds
            },
            "VEHICLE" => new InventoryBackedResourceNeed
            {
                ResourceType = normalizedType,
                CategoryKeyword = TransportationInventoryCategory,
                Quantity = Math.Max(resource.Quantity ?? 1, 1),
                Description = resource.Description,
                Priority = resource.Priority,
                SourceResource = resource,
                SearchTypes = BuildSearchTypeList(resource.Description, "xe", "phuong tien", "xe cuu thuong", "xe khach"),
                RelatedSosIds = relatedSosIds
            },
            "EQUIPMENT" => new InventoryBackedResourceNeed
            {
                ResourceType = normalizedType,
                CategoryKeyword = RescueInventoryCategory,
                Quantity = Math.Max(resource.Quantity ?? 1, 1),
                Description = resource.Description,
                Priority = resource.Priority,
                SourceResource = resource,
                SearchTypes = BuildSearchTypeList(resource.Description, "phao", "day", "cang", "ao phao", "cuu ho"),
                RelatedSosIds = relatedSosIds
            },
            _ => null
        };
    }

    private static bool IsInventoryBackedTransportResource(SuggestedResourceDto resource)
    {
        var normalizedType = (resource.ResourceType ?? string.Empty).Trim().ToUpperInvariant();
        return normalizedType is "BOAT" or "VEHICLE" or "EQUIPMENT";
    }

    private static List<int> ResolveInventoryBackedResourceSosIds(
        IReadOnlyCollection<SuggestedActivityDto> activities,
        IReadOnlyCollection<SosRequestSummary> sosRequests)
    {
        var relatedIds = activities
            .Where(IsOnSiteActivity)
            .SelectMany(activity => GetReferencedSosIds(activity))
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        if (relatedIds.Count > 0)
            return relatedIds;

        return sosRequests
            .Select(sos => sos.Id)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
    }

    private static OperationalTransportSignals InferOperationalTransportSignals(
        IReadOnlyCollection<SosRequestSummary> sosRequests,
        IReadOnlyCollection<SuggestedActivityDto> activities)
    {
        var context = NormalizeItemName(string.Join(
            ' ',
            sosRequests.SelectMany(sos => new[]
            {
                sos.SosType,
                sos.RawMessage,
                sos.StructuredData,
                sos.LatestIncidentNote,
                string.Join(' ', sos.IncidentNotes)
            })));

        var mentionsFlooding = ContainsOperationalKeyword(
            context,
            "ngap",
            "lut",
            "nuoc dang len",
            "nuoc sau",
            "ngap sau",
            "flood",
            "flooded",
            "water level");
        var mentionsIsolation = ContainsOperationalKeyword(
            context,
            "co lap",
            "mac ket",
            "chia cat",
            "khong the tiep can",
            "khong tiep can",
            "trapped",
            "isolated",
            "stranded",
            "cut off");
        var mentionsEvacuation = activities.Any(activity =>
                string.Equals(activity.ActivityType, "EVACUATE", StringComparison.OrdinalIgnoreCase))
            || ContainsOperationalKeyword(
                context,
                "so tan",
                "evacuate",
                "evacuation",
                "di doi",
                "dua ra khoi vung nguy hiem",
                "dua den noi an toan");
        var mentionsRescueGear = activities.Any(activity =>
                string.Equals(activity.ActivityType, "RESCUE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(activity.ActivityType, "MEDICAL_AID", StringComparison.OrdinalIgnoreCase))
            || ContainsOperationalKeyword(
                context,
                "cuu ho",
                "phao",
                "day",
                "cang",
                "sat lo",
                "do nat",
                "rescue");

        return new OperationalTransportSignals(
            RequiresWaterTransport: mentionsFlooding || mentionsIsolation,
            RequiresEvacuationTransport: mentionsEvacuation,
            RequiresRescueEquipment: mentionsFlooding || mentionsIsolation || mentionsRescueGear);
    }

    private static bool ContainsOperationalKeyword(string normalizedText, params string[] keywords) =>
        keywords.Any(keyword => normalizedText.Contains(NormalizeItemName(keyword), StringComparison.Ordinal));

    private static string? SelectTransportNeedPriority(IReadOnlyCollection<SuggestedActivityDto> activities)
    {
        return activities
            .Select(activity => activity.Priority)
            .Where(priority => !string.IsNullOrWhiteSpace(priority))
            .OrderByDescending(GetPriorityRank)
            .FirstOrDefault();
    }

    private static List<string> BuildSearchTypeList(string? description, params string[] fallbacks)
    {
        var results = new List<string>();

        AddSearchType(results, description);
        foreach (var fallback in fallbacks)
            AddSearchType(results, fallback);

        return results;
    }

    private static void AddSearchType(List<string> searchTypes, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var normalized = NormalizeItemName(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        if (searchTypes.Any(existing => string.Equals(NormalizeItemName(existing), normalized, StringComparison.Ordinal)))
            return;

        searchTypes.Add(value.Trim());
    }

    private static List<int> ResolvePreferredDepotIds(
        RescueMissionSuggestionResult result,
        IReadOnlyCollection<DepotSummary> nearbyDepots)
    {
        var activityDepotIds = result.SuggestedActivities
            .Where(activity => activity.DepotId.HasValue)
            .OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue)
            .Select(activity => activity.DepotId!.Value)
            .Distinct()
            .ToList();

        if (activityDepotIds.Count > 0)
            return activityDepotIds.Count == 1 ? activityDepotIds : [activityDepotIds[0]];

        var shortageDepotIds = result.SupplyShortages
            .Where(shortage => shortage.SelectedDepotId.HasValue)
            .Select(shortage => shortage.SelectedDepotId!.Value)
            .Distinct()
            .ToList();

        if (shortageDepotIds.Count > 0)
            return [shortageDepotIds[0]];

        return nearbyDepots
            .Select(depot => depot.Id)
            .Distinct()
            .ToList();
    }

    private async Task<AgentInventoryItem?> FindInventoryBackedResourceMatchAsync(
        InventoryBackedResourceNeed need,
        IReadOnlyList<int> preferredDepotIds,
        IReadOnlyDictionary<int, int> depotPreferenceOrder,
        CancellationToken cancellationToken)
    {
        var candidates = new Dictionary<(int ItemId, int DepotId), AgentInventoryItem>();
        var searchTypes = need.SearchTypes.Count == 0 ? [""] : need.SearchTypes;

        foreach (var searchType in searchTypes)
        {
            var (items, _) = await _depotInventoryRepository.SearchForAgentAsync(
                need.CategoryKeyword,
                string.IsNullOrWhiteSpace(searchType) ? null : searchType,
                page: 1,
                pageSize: AgentPageSize * 5,
                allowedDepotIds: preferredDepotIds,
                ct: cancellationToken);

            foreach (var item in items.Where(item => item.AvailableQuantity > 0))
                candidates[(item.ItemId, item.DepotId)] = item;

            var bestMatch = SelectBestInventoryBackedResourceMatch(candidates.Values, need, depotPreferenceOrder);
            if (bestMatch is not null)
                return bestMatch;
        }

        if (candidates.Count > 0)
            return SelectBestInventoryBackedResourceMatch(candidates.Values, need, depotPreferenceOrder);

        return null;
    }

    private static AgentInventoryItem? SelectBestInventoryBackedResourceMatch(
        IEnumerable<AgentInventoryItem> candidates,
        InventoryBackedResourceNeed need,
        IReadOnlyDictionary<int, int> depotPreferenceOrder)
    {
        var normalizedDescription = NormalizeItemName(need.Description ?? string.Empty);

        return candidates
            .Where(item => item.AvailableQuantity > 0)
            .OrderBy(item => depotPreferenceOrder.TryGetValue(item.DepotId, out var order) ? order : int.MaxValue)
            .ThenByDescending(item => ScoreInventoryBackedResourceMatch(item, need, normalizedDescription))
            .ThenByDescending(item => item.AvailableQuantity >= Math.Max(need.Quantity, 1))
            .ThenByDescending(item => item.AvailableQuantity)
            .ThenByDescending(item => item.GoodAvailableCount ?? 0)
            .ThenBy(item => item.ItemName)
            .FirstOrDefault();
    }

    private static int ScoreInventoryBackedResourceMatch(
        AgentInventoryItem item,
        InventoryBackedResourceNeed need,
        string normalizedDescription)
    {
        var normalizedItemName = NormalizeItemName(item.ItemName);
        var normalizedCategoryName = NormalizeItemName(item.CategoryName);
        var score = 0;

        if (!string.IsNullOrWhiteSpace(normalizedDescription))
        {
            if (normalizedItemName.Contains(normalizedDescription, StringComparison.Ordinal)
                || normalizedDescription.Contains(normalizedItemName, StringComparison.Ordinal))
            {
                score += 80;
            }

            foreach (var token in normalizedDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token.Length >= 3 && normalizedItemName.Contains(token, StringComparison.Ordinal))
                    score += 10;
            }
        }

        score += need.ResourceType switch
        {
            "BOAT" when ContainsOperationalKeyword(normalizedItemName, "ca no", "cano", "canoe", "xuong", "thuyen", "boat") => 100,
            "VEHICLE" when ContainsOperationalKeyword(normalizedItemName, "xe", "truck", "ambulance", "cuu thuong", "xe khach") => 100,
            "EQUIPMENT" when ContainsOperationalKeyword(normalizedItemName, "phao", "day", "cang", "ao phao", "cuu ho") => 100,
            _ => 0
        };

        if (normalizedCategoryName.Contains(NormalizeItemName(need.CategoryKeyword), StringComparison.Ordinal))
            score += 20;

        return score;
    }

    private static SuggestedActivityDto? FindCollectActivityForInventoryBackedResource(
        IReadOnlyCollection<SuggestedActivityDto> activities,
        int depotId,
        IReadOnlyCollection<int> relatedSosIds)
    {
        var collectActivities = activities
            .Where(IsCollectActivity)
            .Where(activity => activity.DepotId == depotId)
            .OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue)
            .ToList();

        if (collectActivities.Count == 0)
            return null;

        return collectActivities.FirstOrDefault(activity =>
                relatedSosIds.Count == 0
                || (activity.SosRequestId.HasValue && relatedSosIds.Contains(activity.SosRequestId.Value))
                || relatedSosIds.Any(id => GetReferencedSosIds(activity).Contains(id)))
            ?? collectActivities[0];
    }

    private static SuggestedActivityDto CreateInventoryBackedCollectActivity(
        AgentInventoryItem item,
        InventoryBackedResourceNeed need,
        int quantity)
    {
        var activity = new SuggestedActivityDto
        {
            Step = 0,
            SuppliesToCollect = []
        };

        ApplyInventoryBackedCollectDefaults(activity, item, need);
        AddOrUpdateInventoryBackedSupply(activity, item, quantity);
        activity.Description = AppendInventoryBackedCollectDescription(
            activity.Description,
            item,
            quantity,
            need.RelatedSosIds);

        return activity;
    }

    private static void ApplyInventoryBackedCollectDefaults(
        SuggestedActivityDto activity,
        AgentInventoryItem item,
        InventoryBackedResourceNeed need)
    {
        activity.ActivityType = CollectSuppliesActivityType;
        activity.DepotId ??= item.DepotId;
        activity.DepotName ??= item.DepotName;
        activity.DepotAddress ??= item.DepotAddress;
        activity.Priority = SelectHigherPriority(activity.Priority, need.Priority);
        activity.EstimatedTime ??= DefaultInventoryBackedCollectEstimatedTime;
        activity.ExecutionMode ??= SingleTeamExecutionMode;
        activity.RequiredTeamCount ??= 1;
        activity.CoordinationNotes ??= "Lay phuong tien/thiet bi huu hinh tu kho de ho tro hien truong.";
        activity.DestinationName ??= item.DepotName;
        activity.DestinationLatitude ??= item.DepotLatitude;
        activity.DestinationLongitude ??= item.DepotLongitude;

        if (!activity.SosRequestId.HasValue && need.RelatedSosIds.Count > 0)
            activity.SosRequestId = need.RelatedSosIds[0];

        activity.SuppliesToCollect ??= [];
    }

    private static void AddOrUpdateInventoryBackedSupply(
        SuggestedActivityDto activity,
        AgentInventoryItem item,
        int quantity)
    {
        activity.SuppliesToCollect ??= [];

        var existingSupply = activity.SuppliesToCollect.FirstOrDefault(supply =>
            (supply.ItemId.HasValue && supply.ItemId.Value == item.ItemId)
            || string.Equals(NormalizeItemName(supply.ItemName), NormalizeItemName(item.ItemName), StringComparison.Ordinal));

        if (existingSupply is null)
        {
            activity.SuppliesToCollect.Add(new SupplyToCollectDto
            {
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                Quantity = quantity,
                Unit = item.Unit
            });
            return;
        }

        existingSupply.ItemId ??= item.ItemId;
        if (string.IsNullOrWhiteSpace(existingSupply.ItemName))
            existingSupply.ItemName = item.ItemName;
        existingSupply.Unit ??= item.Unit;
        existingSupply.Quantity = Math.Max(existingSupply.Quantity, quantity);
    }

    private static string AppendInventoryBackedCollectDescription(
        string? existingDescription,
        AgentInventoryItem item,
        int quantity,
        IReadOnlyCollection<int> relatedSosIds)
    {
        var unitSuffix = string.IsNullOrWhiteSpace(item.Unit) ? string.Empty : $" {item.Unit}";
        var sosSuffix = relatedSosIds.Count == 0
            ? string.Empty
            : $" cho SOS ID {string.Join(", SOS ID ", relatedSosIds.OrderBy(id => id))}";
        var addition = $"Lay {item.ItemName} x{quantity}{unitSuffix}{sosSuffix}.";

        if (string.IsNullOrWhiteSpace(existingDescription))
        {
            var depotLabel = string.IsNullOrWhiteSpace(item.DepotName)
                ? $"kho #{item.DepotId}"
                : item.DepotName;
            return $"Di chuyen den {depotLabel} va {addition.ToLowerInvariant()}";
        }

        if (existingDescription.Contains(item.ItemName, StringComparison.OrdinalIgnoreCase))
            return existingDescription;

        return $"{existingDescription.TrimEnd().TrimEnd('.')}. Bo sung tu kho: {item.ItemName} x{quantity}{unitSuffix}{sosSuffix}.";
    }

    private static void TrimInventoryBackedSuggestedResource(
        List<SuggestedResourceDto> suggestedResources,
        SuggestedResourceDto? sourceResource,
        int coveredQuantity)
    {
        if (sourceResource is null)
            return;

        var resource = suggestedResources.FirstOrDefault(item => ReferenceEquals(item, sourceResource))
            ?? suggestedResources.FirstOrDefault(item =>
                string.Equals(item.ResourceType, sourceResource.ResourceType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Description, sourceResource.Description, StringComparison.OrdinalIgnoreCase));

        if (resource is null)
            return;

        if (!resource.Quantity.HasValue || resource.Quantity.Value <= coveredQuantity)
        {
            suggestedResources.Remove(resource);
            return;
        }

        resource.Quantity = Math.Max(resource.Quantity.Value - coveredQuantity, 0);
        if (resource.Quantity == 0)
            suggestedResources.Remove(resource);
    }

    private static void ApplyMixedRescueReliefSafetyNote(RescueMissionSuggestionResult result)
    {
        var warning = MissionSuggestionWarningHelper.BuildMixedRescueReliefWarning(result.SuggestedActivities);
        if (string.IsNullOrWhiteSpace(warning))
            return;

        result.NeedsManualReview = true;
        result.MixedRescueReliefWarning = warning;
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

    private static bool IsPipelinePromptType(PromptType promptType) =>
        promptType is PromptType.MissionRequirementsAssessment
            or PromptType.MissionDepotPlanning
            or PromptType.MissionTeamPlanning
            or PromptType.MissionPlanValidation;

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

    private static SseMissionEvent Error(string msg, RescueMissionSuggestionResult? result = null) =>
        new() { EventType = "error", Data = msg, Result = result };

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

        if (options.PromptOverride is not null
            && !IsPipelinePromptType(options.PromptOverride.PromptType))
        {
            var unsupportedPromptResult = CreateFailureResult(
                $"Prompt type '{options.PromptOverride.PromptType}' khong con duoc ho tro cho mission suggestion pipeline.");
            yield return Error(unsupportedPromptResult.ErrorMessage ?? "Mission suggestion pipeline failed.", unsupportedPromptResult);
            yield break;
        }

        var aiConfig = await GetEffectiveAiConfigAsync(options, cancellationToken);
        if (aiConfig == null)
        {
            var missingConfigResult = CreateFailureResult(
                "Chua co AI config active trong he thong. Vui long kich hoat AI config truoc khi chay prompt.");
            yield return Error(missingConfigResult.ErrorMessage ?? "Mission suggestion pipeline failed.", missingConfigResult);
            yield break;
        }

        var pipelineMetadata = CreateSuggestionMetadataForPipeline();
        var suggestionId = options.PersistSuggestion
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

        RescueMissionSuggestionResult? failedResult = null;
        string? failedMessage = null;

        while (true)
        {
            bool movedNext;
            try
            {
                movedNext = await pipelineEnumerator.MoveNextAsync();
            }
            catch (MissionSuggestionPipelineException ex)
            {
                failedResult = await CreateFailedSuggestionResultAsync(
                    ex.FailedStage,
                    ex.FailureReason,
                    suggestionId,
                    pipelineMetadata,
                    options,
                    cancellationToken);
                failedMessage = failedResult.ErrorMessage ?? ex.FailureReason;
                _logger.LogWarning(ex, "Mission suggestion pipeline failed at stage {stage}", ex.FailedStage);
                break;
            }

            if (!movedNext)
                break;

            yield return pipelineEnumerator.Current;
        }

        if (failedResult is not null)
            yield return Error(failedMessage ?? "Mission suggestion pipeline failed.", failedResult);

        yield break;
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

        [JsonPropertyName("warning_level")]
        public string? WarningLevel { get; set; }

        [JsonPropertyName("warning_title")]
        public string? WarningTitle { get; set; }

        [JsonPropertyName("warning_message")]
        public string? WarningMessage { get; set; }

        [JsonPropertyName("warning_related_sos_ids")]
        public List<int>? WarningRelatedSosIds { get; set; }

        [JsonPropertyName("warning_reason")]
        public string? WarningReason { get; set; }

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
