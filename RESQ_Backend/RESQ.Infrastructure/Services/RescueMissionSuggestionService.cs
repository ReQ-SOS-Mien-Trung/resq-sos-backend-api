using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Domain.Enum.System;

namespace RESQ.Infrastructure.Services;

public class RescueMissionSuggestionService : IRescueMissionSuggestionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPromptRepository _promptRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository;
    private readonly IRescueTeamRepository _rescueTeamRepository;
    private readonly ILogger<RescueMissionSuggestionService> _logger;

    // Fallback defaults
    private const string FallbackModel = "gemini-2.5-flash";
    private const string FallbackApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
    private const double FallbackTemperature = 0.5;
    private const int FallbackMaxTokens = 65535;

    // Agent loop constants
    private const int MaxAgentTurns = 20;
    private const int AgentPageSize = 10;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public RescueMissionSuggestionService(
        IHttpClientFactory httpClientFactory,
        IPromptRepository promptRepository,
        IDepotInventoryRepository depotInventoryRepository,
        IRescueTeamRepository rescueTeamRepository,
        ILogger<RescueMissionSuggestionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _promptRepository = promptRepository;
        _depotInventoryRepository = depotInventoryRepository;
        _rescueTeamRepository = rescueTeamRepository;
        _logger = logger;
    }

    public async Task<RescueMissionSuggestionResult> GenerateSuggestionAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots = null,
        bool isMultiDepotRecommended = false,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        RescueMissionSuggestionResult? finalResult = null;

        try
        {
            await foreach (var evt in GenerateSuggestionStreamAsync(
                sosRequests, nearbyDepots, isMultiDepotRecommended, cancellationToken))
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
        var entries = sosRequests.Select((sos, index) => new
        {
            stt = index + 1,
            id = sos.Id,
            loai_sos = sos.SosType ?? "Không xác định",
            tin_nhan = sos.RawMessage,
            du_lieu_chi_tiet = sos.StructuredData ?? "Không có",
            muc_uu_tien = sos.PriorityLevel ?? "Chưa đánh giá",
            trang_thai = sos.Status ?? "Không rõ",
            vi_tri = sos.Latitude.HasValue && sos.Longitude.HasValue
                ? $"{sos.Latitude}, {sos.Longitude}"
                : "Không xác định",
            thoi_gian_cho_doi_phut = sos.CreatedAt.HasValue
                ? (int)(now - sos.CreatedAt.Value).TotalMinutes
                : (int?)null,
            thoi_gian_tao = sos.CreatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"
        });

        return JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static string BuildAgentInstructions(bool isMultiDepotRecommended = false)
    {
        var multiDepotSection = isMultiDepotRecommended
            ? """

              ## CHẾ ĐỘ ĐA KHO (MULTI-DEPOT) — BẮT BUỘC ÁP DỤNG
              Hệ thống đã xác định rằng KHÔNG CÓ KHO ĐƠN LẺ NÀO có thể cung cấp đủ tất cả vật tư cần thiết. Bạn BẮT BUỘC phải sử dụng nhiều kho.

              **Bước 1 — Thu thập dữ liệu đa kho**:
              - Gọi searchInventory cho TỪNG danh mục riêng lẻ, và nếu `total_pages > 1`, gọi tiếp các trang sau để lấy đủ dữ liệu từ tất cả các kho.
              - Sau khi thu thập xong, lập bảng phân tích: với từng loại vật tư cần thiết, liệt kê **từng kho** có vật tư đó và `available_quantity` của kho đó.

              **Bước 2 — Xác định kho cho từng loại vật tư**:
              - Với mỗi loại vật tư, chọn kho có đủ số lượng và gần sự cố nhất.
              - Nếu không có kho nào đủ số lượng cho một loại vật tư, phân bổ từ nhiều kho: lấy hết tại kho A rồi lấy phần còn thiếu tại kho B.
              - Ví dụ: Cần 360 gói mì tôm. Kho A có 200, Kho B có 200 → lấy 200 từ Kho A (step 1), lấy 160 từ Kho B (step 2).
              - Ví dụ: Cần 120 chai nước (Kho A có đủ 120), cần 4 gói sữa (chỉ Kho B có) → 2 bước COLLECT từ 2 kho khác nhau.

              **Bước 3 — Tạo bước COLLECT_SUPPLIES tách biệt theo kho**:
              - Mỗi kho = một bước COLLECT_SUPPLIES riêng với đúng `depot_id`, `depot_name`, `depot_address` của kho đó.
              - `supplies_to_collect` trong bước đó chỉ chứa vật tư thực tế lấy từ KHO ĐÓ với số lượng thực tế lấy.
              - Ghi rõ trong `description`: "Di chuyển đến kho [Tên kho]. Lấy: [Vật tư A] x[qty], [Vật tư B] x[qty]".
              - **TUYỆT ĐỐI KHÔNG** gộp vật tư từ nhiều kho vào cùng một bước COLLECT_SUPPLIES.

              **Lưu ý khi xuất JSON**:
              - Ưu tiên dùng nhiều kho khác nhau nếu dữ liệu searchInventory trả về vật tư từ nhiều kho. Nếu chỉ tìm thấy 1 kho có vật tư, thì 1 kho là chấp nhận được — ghi chú trong `special_notes`: "Chỉ tìm thấy vật tư tại 1 kho trong khu vực".

              """
            : string.Empty;

        return $$"""
            ## HƯỚNG DẪN SỬ DỤNG CÔNG CỤ
            Bạn có thể gọi hai công cụ để tìm kiếm dữ liệu thực từ hệ thống trước khi lập kế hoạch:

            - **searchInventory(category, type?, page)**: Tìm kiếm vật tư trong kho theo danh mục (ví dụ: "Nước", "Thực phẩm", "Y tế", "Cứu hộ"). Trả về danh sách vật tư khả dụng kèm theo item_id, tên, **available_quantity** (số lượng thực tế có thể lấy), depot_id, tên kho, địa chỉ kho, depot_latitude, depot_longitude. Mỗi hàng trong kết quả là một (vật tư, kho) riêng biệt — cùng một vật tư có thể xuất hiện ở nhiều hàng nếu có ở nhiều kho khác nhau.
            - **getTeams(ability?, available?, page)**: Tìm kiếm đội cứu hộ. Có thể lọc theo khả năng (team_type) và trạng thái khả dụng. Trả về team_id, tên, loại, trạng thái, số thành viên và vị trí điểm tập kết (assembly_point_name, latitude, longitude).

            **BẮT BUỘC trước khi lập kế hoạch**:
            - Gọi **searchInventory** cho TỪNG danh mục: Thực phẩm, Nước, Y tế, Cứu hộ (và các danh mục khác phù hợp). Không bỏ qua danh mục nào có thể liên quan.
            - Gọi **getTeams** để lấy team_id cho `suggested_team`.
            - Dùng đúng item_id, depot_id, team_id từ kết quả — KHÔNG tự tạo ID.
            {{multiDepotSection}}
            ## QUY TẮC KIỂM TRA VÀ BÁO CÁO THIẾU HỤT VẬT TƯ (BẮT BUỘC)
            Sau khi nhận kết quả searchInventory, PHẢI so sánh `available_quantity` của từng vật tư với số lượng cần thiết:

            **Trường hợp 1 — Đủ hàng** (`tổng available_quantity từ các kho >= needed_quantity`):
            - Điền đúng số lượng cần vào `supplies_to_collect.quantity`.

            **Trường hợp 2 — Thiếu một phần** (`0 < tổng available_quantity < needed_quantity`):
            - `supplies_to_collect.quantity` = số lượng thực tế lấy được (= tổng available, KHÔNG phải needed).
            - BẮT BUỘC ghi vào `special_notes`:
              `"[SOS ID X]: Thiếu [TÊN VẬT TƯ] x[SỐ LƯỢNG THIẾU] [đơn vị] (kho chỉ có [tổng_available]/[needed_quantity] [đơn vị])"`

            **Trường hợp 3 — Không có trong kho** (không tìm thấy vật tư trong bất kỳ kho nào):
            - KHÔNG tạo bước COLLECT_SUPPLIES cho vật tư này.
            - BẮT BUỘC ghi vào `special_notes`: `"[SOS ID X]: Không có [TÊN VẬT TƯ] trong hệ thống kho"`
            - **PHÂN BIỆT RÕ**: "Không có trong kho" khác với "thiếu một phần" — KHÔNG viết "kho chỉ có 0/X" cho trường hợp này.

            ## QUY TẮC CHO TỪNG LOẠI ACTIVITY

            ### COLLECT_SUPPLIES
            - Chỉ tạo khi có vật tư thực tế trong kho (available_quantity > 0).
            - `depot_id`, `depot_name`, `depot_address` phải khớp với kho thực tế trả về từ searchInventory.
            - `supplies_to_collect` chỉ chứa vật tư lấy từ kho đó với số lượng thực tế lấy.

            ### DELIVER_SUPPLIES
            - Tạo sau mỗi COLLECT_SUPPLIES để giao hàng đến điểm sự cố.
            - `supplies_to_collect` liệt kê đúng những gì đã lấy từ bước COLLECT tương ứng.

            ### RESCUE
            - **LUÔN tạo bước RESCUE** ngay cả khi thiếu thiết bị cứu hộ.
            - Nếu cần thiết bị cứu hộ chuyên dụng (dụng cụ phá dỡ, thiết bị nâng đỡ v.v.) và có trong kho → đưa vào `supplies_to_collect`.
            - Nếu thiết bị cần thiết KHÔNG có trong kho → tạo thêm bước **REQUEST_SUPPORT** ngay sau bước RESCUE:
              - `activity_type`: "REQUEST_SUPPORT"
              - `description`: Ghi rõ thiết bị còn thiếu và đề nghị hỗ trợ từ cơ quan chức năng (PCCC, đơn vị tìm kiếm cứu nạn chuyên dụng, cơ sở y tế gần nhất).
              - `supplies_to_collect`: null
              - `priority`: cùng priority với bước RESCUE

            ### MEDICAL_AID
            - **LUÔN có `supplies_to_collect`** nếu tình huống cần vật tư y tế (sơ cứu, thuốc, dụng cụ y tế).
            - Trước khi tạo bước MEDICAL_AID, PHẢI gọi searchInventory với danh mục "Y tế" để lấy danh sách vật tư y tế khả dụng.
            - Nếu có vật tư y tế trong kho → điền vào `supplies_to_collect` với item_id và depot_id thực tế.
            - Nếu vật tư y tế KHÔNG có hoặc THIẾU → vẫn tạo bước MEDICAL_AID, để `supplies_to_collect: null`, và ghi vào `special_notes` rằng thiếu vật tư y tế cụ thể nào.
            - Đừng bỏ qua bước COLLECT_SUPPLIES cho vật tư y tế nếu có trong kho — phải lấy trước khi thực hiện MEDICAL_AID.

            ### EVACUATE
            - Tạo khi cần vận chuyển người bị thương đến cơ sở y tế.
            - `supplies_to_collect`: null (không lấy vật tư ở bước này).

            **QUY TẮC RETRY khi tìm đội (rất quan trọng)**:
            - Luôn thử `getTeams(available=true, page=1)` trước để ưu tiên đội sẵn sàng.
            - Nếu kết quả trả về `total_teams = 0` hoặc `teams` rỗng, bắt buộc gọi lại `getTeams(available=false, page=1)` để lấy danh sách đội không bị giải thể.
            - Nếu bạn có dùng lọc `ability` mà không thấy đội nào, gọi lại `getTeams` **không truyền `ability`** để lấy tất cả đội.
            - Chỉ khi đã thử các bước trên mà vẫn không có đội nào, lúc đó mới được để `suggested_team` = null.

            ## SỬ DỤNG VỊ TRÍ ĐỂ LẬP KẾ HOẠCH
            Mỗi SOS request có trường `vi_tri` chứa tọa độ (latitude, longitude) của sự cố.
            Kết quả searchInventory trả về `depot_latitude`, `depot_longitude` — tọa độ của kho vật tư.
            Kết quả getTeams trả về `latitude`, `longitude` — tọa độ điểm tập kết của đội cứu hộ.

            **Quy tắc sử dụng vị trí**:
            - Ưu tiên chọn kho vật tư **gần nhất** với vị trí sự cố (so sánh tọa độ).
            - Ưu tiên chọn đội cứu hộ có điểm tập kết **gần nhất** với vị trí sự cố.
            - Khi có nhiều sự cố, phân công đội và kho sao cho quãng đường di chuyển tổng cộng là nhỏ nhất.
            - Ghi rõ lý do chọn kho và đội dựa trên vị trí địa lý trong trường `reason` và `description`.

            **Trường bắt buộc trong suggested_team**: Ngoài team_id, team_name, team_type và reason, bạn **phải** điền thêm:
            - `assembly_point_name`: tên điểm tập kết (lấy từ kết quả getTeams)
            - `latitude`: vĩ độ điểm tập kết (lấy từ kết quả getTeams)
            - `longitude`: kinh độ điểm tập kết (lấy từ kết quả getTeams)
            Nếu đội không có điểm tập kết (giá trị null trong kết quả), hãy để các trường đó là null.

            ## QUY TẮC PHÂN CÔNG ĐỘI VÀO ACTIVITY
            - **MỖI activity PHẢI có trường `suggested_team`** — không được để null trừ khi thực sự không tìm được đội nào.
            - Sau khi gọi getTeams, phân công đội phù hợp vào từng activity dựa trên loại hoạt động và vị trí.
            - Nếu một đội đảm nhận nhiều activity, điền cùng một đội vào `suggested_team` của từng activity đó.
            - **KHÔNG** chỉ điền đội vào mảng `resources` rồi để `suggested_team` là null trong activities.
            - Nếu không có đủ đội cho tất cả activity, ưu tiên gán đội cho các bước có `priority = Critical` và các bước RESCUE/MEDICAL_AID/EVACUATE trước.
            - Format `suggested_team` bên trong mỗi activity:
              ```json
              "suggested_team": {
                "team_id": 5,
                "team_name": "Đội Phản ứng nhanh Quảng Bình",
                "team_type": "RescueTeam",
                "reason": "Gần nhất với sự cố, có khả năng y tế",
                "assembly_point_name": "Trụ sở PCCC Quảng Bình",
                "latitude": 17.46,
                "longitude": 106.62
              }
              ```

            ## QUY TẮC LẬP KẾ HOẠCH
            - Không lập kế hoạch tuần tự nếu có nhiều sự cố.
            - Nếu có nhiều SOS request, hãy phân chia đội cứu hộ xử lý song song.
            - Mỗi đội chỉ nên phụ trách một khu vực hoặc một sự cố.
            - Ưu tiên xử lý sự cố có người bị thương nặng trước.

            ## QUY TẮC SỬ DỤNG ID
            - KHÔNG được tự tạo item_id hoặc team_id.
            - Chỉ sử dụng ID xuất hiện trong kết quả tool.
            - Nếu không tìm thấy vật tư phù hợp, hãy ghi rõ "Không có sẵn".

            ## ĐỊNH DẠNG overall_assessment
            - Toàn bộ nội dung phải là một chuỗi văn bản liên tục trên MỘT DÒNG DUY NHẤT — KHÔNG được chèn `\n`, xuống dòng, hoặc ký tự xuống dòng bất kỳ.
            - Khi đề cập từng sự cố, dùng định dạng `[SOS ID X]:` (trong đó X là giá trị `id` của SOS request).
            - Phân cách giữa các sự cố bằng dấu cách thông thường, KHÔNG dùng `\n`.
            - KHÔNG dùng "SOS 1 (ID X):" hoặc các biến thể đánh số thứ tự khác.
            - Ví dụ đúng: "[SOS ID 4]: 120 người bị cô lập... [SOS ID 3]: 5 người bị nạn..."
            - Ví dụ sai: "[SOS ID 4]: 120 người bị cô lập...\n[SOS ID 3]: 5 người bị nạn..."
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
                SosRequestId = a.SosRequestId,
                DepotId = a.DepotId,
                DepotName = a.DepotName,
                DepotAddress = a.DepotAddress,
                SuppliesToCollect = a.SuppliesToCollect?.Select(s => new SupplyToCollectDto
                {
                    ItemId = s.ItemId,
                    ItemName = s.ItemName ?? string.Empty,
                    Quantity = s.Quantity,
                    Unit = s.Unit
                }).ToList(),
                SuggestedTeam = a.SuggestedTeam == null ? null : new SuggestedTeamDto
                {
                    TeamId            = a.SuggestedTeam.TeamId,
                    TeamName          = a.SuggestedTeam.TeamName ?? string.Empty,
                    TeamType          = a.SuggestedTeam.TeamType,
                    Reason            = a.SuggestedTeam.Reason,
                    AssemblyPointName = a.SuggestedTeam.AssemblyPointName,
                    Latitude          = a.SuggestedTeam.Latitude,
                    Longitude         = a.SuggestedTeam.Longitude
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
                AssemblyPointName  = parsed.SuggestedTeam.AssemblyPointName,
                Latitude           = parsed.SuggestedTeam.Latitude,
                Longitude          = parsed.SuggestedTeam.Longitude
            },
            EstimatedDuration = parsed.EstimatedDuration,
            SpecialNotes = parsed.SpecialNotes,
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
                if (a.TryGetProperty("sos_request_id", out var sri) && sri.ValueKind != JsonValueKind.Null && sri.TryGetInt32(out var sriv)) dto.SosRequestId = sriv;
                if (a.TryGetProperty("depot_id", out var di) && di.ValueKind != JsonValueKind.Null && di.TryGetInt32(out var div)) dto.DepotId = div;
                if (a.TryGetProperty("depot_name", out var dn) && dn.ValueKind != JsonValueKind.Null) dto.DepotName = dn.GetString();
                if (a.TryGetProperty("depot_address", out var da) && da.ValueKind != JsonValueKind.Null) dto.DepotAddress = da.GetString();
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
                    if (ast.TryGetProperty("assembly_point_name", out var apn)  && apn.ValueKind != JsonValueKind.Null)                                  teamDto.AssemblyPointName = apn.GetString();
                    if (ast.TryGetProperty("latitude",            out var lat)  && lat.ValueKind != JsonValueKind.Null && lat.TryGetDouble(out var latv)) teamDto.Latitude          = latv;
                    if (ast.TryGetProperty("longitude",           out var lon)  && lon.ValueKind != JsonValueKind.Null && lon.TryGetDouble(out var lonv)) teamDto.Longitude         = lonv;
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
            if (st.TryGetProperty("assembly_point_name",out var apn) && apn.ValueKind != JsonValueKind.Null)                                        teamDto.AssemblyPointName = apn.GetString();
            if (st.TryGetProperty("latitude",           out var lat) && lat.ValueKind != JsonValueKind.Null && lat.TryGetDouble(out var latv))       teamDto.Latitude          = latv;
            if (st.TryGetProperty("longitude",          out var lon) && lon.ValueKind != JsonValueKind.Null && lon.TryGetDouble(out var lonv))       teamDto.Longitude         = lonv;
            result.SuggestedTeam = teamDto;
        }

        return result;
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

        return new RescueMissionSuggestionResult
        {
            SuggestedMissionTitle = ExtractStr(text, "mission_title") ?? "Nhiệm vụ giải cứu",
            SuggestedMissionType = ExtractStr(text, "mission_type"),
            SuggestedPriorityScore = ExtractNum(text, "priority_score"),
            SuggestedSeverityLevel = ExtractStr(text, "severity_level"),
            OverallAssessment = ExtractStr(text, "overall_assessment")?.Replace("\n", " ").Replace("\r", " ").Trim(),
            EstimatedDuration = ExtractStr(text, "estimated_duration"),
            SpecialNotes = ExtractStr(text, "special_notes"),
            ConfidenceScore = ExtractNum(text, "confidence_score") ?? 0.3
        };
    }

    // ─── SSE helpers ───────────────────────────────────────────────────────────

    private static SseMissionEvent Status(string msg) =>
        new() { EventType = "status", Data = msg };

    private static SseMissionEvent Error(string msg) =>
        new() { EventType = "error", Data = msg };

    // ─── Streaming (SSE agent loop) ────────────────────────────────────────────

    public async IAsyncEnumerable<SseMissionEvent> GenerateSuggestionStreamAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots = null,
        bool isMultiDepotRecommended = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return Status("Đang tải cấu hình AI agent...");

        var prompt = await _promptRepository.GetActiveByTypeAsync(PromptType.MissionPlanning, cancellationToken);
        if (prompt == null)
        {
            yield return Error("Chưa có prompt 'MissionPlanning' đang được kích hoạt. Vui lòng cấu hình trong quản trị hệ thống.");
            yield break;
        }

        var modelName   = prompt.Model       ?? FallbackModel;
        var apiUrlTpl   = prompt.ApiUrl      ?? FallbackApiUrl;
        var apiKey      = prompt.ApiKey      ?? string.Empty;
        var temperature = prompt.Temperature ?? FallbackTemperature;
        // Enforce minimum 32K tokens — mission plans with tool calls can be very long
        var maxTokens   = Math.Max(prompt.MaxTokens ?? FallbackMaxTokens, 32768);

        var baseUrl = string.Format(apiUrlTpl, modelName, apiKey);

        // Build the initial user message (no pre-loaded depot data; agent fetches via tools)
        var sosDataJson = BuildSosRequestsData(sosRequests);
        var userMessage = (prompt.UserPromptTemplate ?? string.Empty)
            .Replace("{{sos_requests_data}}", sosDataJson)
            .Replace("{{total_count}}", sosRequests.Count.ToString())
            .Replace("{{depots_data}}", "(Dữ liệu kho không được truyền trực tiếp. Hãy gọi công cụ searchInventory để tra cứu vật tư khả dụng theo từng danh mục, rồi dùng dữ liệu đó để lập bước COLLECT_SUPPLIES và DELIVER_SUPPLIES.)")
            .TrimEnd();

        var systemPrompt = (prompt.SystemPrompt ?? string.Empty).TrimEnd()
            + "\n\n" + BuildAgentInstructions(isMultiDepotRecommended);

        yield return Status($"AI agent ({modelName}) đang phân tích {sosRequests.Count} SOS request...");

        var contents = new List<GeminiMultiTurnContent>
        {
            new() { Role = "user", Parts = [new GeminiMultiTurnPart { Text = userMessage }] }
        };

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(120);

        string? finalText = null;

        for (int turn = 0; turn < MaxAgentTurns; turn++)
        {
            if (cancellationToken.IsCancellationRequested) yield break;

            var requestBody = new GeminiRequestWithTools
            {
                SystemInstruction = new GeminiSystemInstruction
                {
                    Parts = [new GeminiMultiTurnPart { Text = systemPrompt }]
                },
                Contents = contents,
                Tools = [BuildToolDeclarations()],
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = temperature,
                    MaxOutputTokens = maxTokens
                }
            };

            var bodyJson = JsonSerializer.Serialize(requestBody, _jsonOpts);

            HttpResponseMessage response = null!;
            string? sendError = null;
            const int maxSendRetries = 3;
            for (int attempt = 0; attempt < maxSendRetries; attempt++)
            {
                // HttpRequestMessage is single-use — rebuild each attempt
                var httpReq = new HttpRequestMessage(HttpMethod.Post, baseUrl)
                {
                    Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
                };
                try
                {
                    sendError = null;
                    response  = await client.SendAsync(httpReq, cancellationToken);
                }
                catch (Exception ex)
                {
                    sendError = ex.Message;
                    response  = null!;
                    break; // Network errors are not retryable here
                }

                if (response.IsSuccessStatusCode || (int)response.StatusCode != 503)
                    break;

                if (attempt < maxSendRetries - 1)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt + 2)); // 4s, 8s, 16s
                    _logger.LogWarning(
                        "Gemini 503 (turn={turn}, attempt={attempt}), retrying in {delay}s…",
                        turn, attempt + 1, (int)delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }

            if (sendError != null)
            {
                yield return Error($"Lỗi kết nối tới AI: {sendError}");
                yield break;
            }

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini API error turn={turn}: {status} – {error}", turn, response.StatusCode, err);
                yield return Error($"AI trả về lỗi ({response.StatusCode}). Vui lòng thử lại sau.");
                yield break;
            }

            GeminiMultiTurnResponse? geminiResp;
            string? responseJson = null;
            string? parseError = null;
            try
            {
                responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                geminiResp = JsonSerializer.Deserialize<GeminiMultiTurnResponse>(responseJson, _jsonOpts);
            }
            catch (Exception ex)
            {
                parseError = ex.Message;
                geminiResp = null;
            }
            if (parseError != null)
            {
                yield return Error($"Không thể phân tích phản hồi AI: {parseError}");
                yield break;
            }

            // Check for prompt-level block before looking at candidates
            if (geminiResp?.PromptFeedback?.BlockReason is string blockReason && blockReason != "BLOCK_REASON_UNSPECIFIED")
            {
                _logger.LogWarning("Gemini prompt blocked (turn={turn}): BlockReason={reason}", turn, blockReason);
                yield return Error($"Yêu cầu bị chặn bởi bộ lọc AI ({blockReason}). Vui lòng thử lại hoặc điều chỉnh nội dung SOS.");
                yield break;
            }

            var candidate  = geminiResp?.Candidates?.FirstOrDefault();
            var modelParts = candidate?.Content?.Parts;

            if (modelParts == null || modelParts.Count == 0)
            {
                var finishReason = candidate?.FinishReason ?? "(no candidate)";
                _logger.LogWarning(
                    "Gemini returned empty content (turn={turn}), finishReason={reason}. Raw snippet: {raw}",
                    turn, finishReason,
                    responseJson?.Length > 500 ? responseJson[..500] : responseJson);

                // Retry once on transient failures, otherwise surface the error
                if (finishReason is "SAFETY" or "RECITATION" or "OTHER" or "BLOCKLIST" or "PROHIBITED_CONTENT")
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

            // Append model response to conversation
            contents.Add(new GeminiMultiTurnContent { Role = "model", Parts = modelParts });

            var functionCallParts = modelParts.Where(p => p.FunctionCall != null).ToList();

            if (functionCallParts.Count == 0)
            {
                // No function calls → final answer
                finalText = string.Concat(modelParts.Where(p => p.Text != null).Select(p => p.Text));
                break;
            }

            // Execute each function call
            var responseParts = new List<GeminiMultiTurnPart>();
            foreach (var part in functionCallParts)
            {
                var fc = part.FunctionCall!;
                yield return Status($"Agent đang gọi công cụ: {fc.Name}(...)");

                JsonElement toolResult;
                try
                {
                    toolResult = await ExecuteToolAsync(fc.Name, fc.Args, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Tool {name} threw an exception", fc.Name);
                    toolResult = JsonSerializer.SerializeToElement(new { error = ex.Message });
                }

                yield return Status($"Công cụ {fc.Name}() đã trả về kết quả.");

                responseParts.Add(new GeminiMultiTurnPart
                {
                    FunctionResponse = new GeminiFunctionResponse { Name = fc.Name, Response = toolResult }
                });
            }

            contents.Add(new GeminiMultiTurnContent { Role = "user", Parts = responseParts });
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
        result.ModelName     = modelName;
        result.RawAiResponse = finalText;

        _logger.LogInformation(
            "Agent mission suggestion: Title={title}, Type={type}, Activities={count}, Team={team}, Confidence={conf}",
            result.SuggestedMissionTitle, result.SuggestedMissionType,
            result.SuggestedActivities.Count,
            result.SuggestedTeam?.TeamName ?? "none",
            result.ConfidenceScore);

        yield return new SseMissionEvent { EventType = "result", Result = result };
    }

    // ─── Tool execution ────────────────────────────────────────────────────────

    private async Task<JsonElement> ExecuteToolAsync(string toolName, JsonElement args, CancellationToken ct)
    {
        switch (toolName)
        {
            case "searchInventory":
            {
                var category = args.TryGetProperty("category", out var c) ? c.GetString() ?? string.Empty : string.Empty;
                var type     = args.TryGetProperty("type",     out var t) ? t.GetString() : null;
                var page     = args.TryGetProperty("page",     out var p) && p.TryGetInt32(out var pv) ? pv : 1;

                var (items, total) = await _depotInventoryRepository.SearchForAgentAsync(
                    category, type, page, AgentPageSize, ct);

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
                bool? availFinal = null;
                if (args.TryGetProperty("available", out var availElem))
                {
                    if (availElem.ValueKind == JsonValueKind.True)  availFinal = true;
                    if (availElem.ValueKind == JsonValueKind.False) availFinal = false;
                }
                var page = args.TryGetProperty("page", out var pg) && pg.TryGetInt32(out var pgv) ? pgv : 1;

                var (teams, total) = await _rescueTeamRepository.GetTeamsForAgentAsync(
                    ability, availFinal, page, AgentPageSize, ct);

                var totalPages = (int)Math.Ceiling((double)total / AgentPageSize);
                return JsonSerializer.SerializeToElement(new
                {
                    teams,
                    page,
                    total_pages = totalPages,
                    total_teams = total
                }, _jsonOpts);
            }

            default:
                return JsonSerializer.SerializeToElement(new { error = $"Unknown tool: {toolName}" });
        }
    }

    #region Gemini API Models

    private static GeminiTool BuildToolDeclarations() => new()
    {
        FunctionDeclarations =
        [
            new()
            {
                Name        = "searchInventory",
                Description = "Tìm kiếm vật tư đang khả dụng trong kho theo danh mục và loại. Trả về danh sách vật tư kèm item_id, tên, số lượng, kho chứa và tọa độ vị trí kho (depot_latitude, depot_longitude).",
                Parameters  = new GeminiFunctionParameters
                {
                    Type       = "object",
                    Properties = new Dictionary<string, GeminiFunctionProperty>
                    {
                        ["category"] = new() { Type = "string",  Description = "Tên danh mục vật tư, ví dụ: 'Nước', 'Thực phẩm', 'Y tế', 'Quần áo'" },
                        ["type"]     = new() { Type = "string",  Description = "Loại cụ thể trong danh mục (tuỳ chọn)" },
                        ["page"]     = new() { Type = "integer", Description = "Số trang (bắt đầu từ 1)" }
                    },
                    Required = ["category"]
                }
            },
            new()
            {
                Name        = "getTeams",
                Description = "Tìm kiếm đội cứu hộ trong hệ thống. Có thể lọc theo loại kỹ năng và trạng thái sẵn sàng. Trả về team_id, tên, loại, trạng thái, số thành viên và vị trí điểm tập kết (assembly_point_name, latitude, longitude).",
                Parameters  = new GeminiFunctionParameters
                {
                    Type       = "object",
                    Properties = new Dictionary<string, GeminiFunctionProperty>
                    {
                        ["ability"]   = new() { Type = "string",  Description = "Lọc theo loại kỹ năng/team_type (tuỳ chọn)" },
                        ["available"] = new() { Type = "boolean", Description = "Nếu true chỉ trả về đội đang Available hoặc Ready" },
                        ["page"]      = new() { Type = "integer", Description = "Số trang (bắt đầu từ 1)" }
                    },
                    Required = []
                }
            }
        ]
    };

    private class GeminiRequestWithTools
    {
        [JsonPropertyName("system_instruction")]
        public GeminiSystemInstruction? SystemInstruction { get; set; }

        [JsonPropertyName("contents")]
        public List<GeminiMultiTurnContent> Contents { get; set; } = [];

        [JsonPropertyName("tools")]
        public List<GeminiTool> Tools { get; set; } = [];

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private class GeminiSystemInstruction
    {
        [JsonPropertyName("parts")]
        public List<GeminiMultiTurnPart> Parts { get; set; } = [];
    }

    private class GeminiMultiTurnContent
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("parts")]
        public List<GeminiMultiTurnPart> Parts { get; set; } = [];
    }

    private class GeminiMultiTurnPart
    {
        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; set; }

        [JsonPropertyName("functionCall")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiFunctionCall? FunctionCall { get; set; }

        [JsonPropertyName("functionResponse")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiFunctionResponse? FunctionResponse { get; set; }
    }

    private class GeminiFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("args")]
        public JsonElement Args { get; set; }
    }

    private class GeminiFunctionResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("response")]
        public JsonElement Response { get; set; }
    }

    private class GeminiTool
    {
        [JsonPropertyName("functionDeclarations")]
        public List<GeminiFunctionDeclaration> FunctionDeclarations { get; set; } = [];
    }

    private class GeminiFunctionDeclaration
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public GeminiFunctionParameters? Parameters { get; set; }
    }

    private class GeminiFunctionParameters
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "object";

        [JsonPropertyName("properties")]
        public Dictionary<string, GeminiFunctionProperty> Properties { get; set; } = [];

        [JsonPropertyName("required")]
        public List<string> Required { get; set; } = [];
    }

    private class GeminiFunctionProperty
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "string";

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    private class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }
    }

    private class GeminiMultiTurnResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiMultiTurnCandidate>? Candidates { get; set; }

        [JsonPropertyName("promptFeedback")]
        public GeminiPromptFeedback? PromptFeedback { get; set; }
    }

    private class GeminiPromptFeedback
    {
        [JsonPropertyName("blockReason")]
        public string? BlockReason { get; set; }
    }

    private class GeminiMultiTurnCandidate
    {
        [JsonPropertyName("content")]
        public GeminiMultiTurnContent? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
    }

    #endregion

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

        [JsonPropertyName("confidence_score")]
        public double ConfidenceScore { get; set; }
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

        [JsonPropertyName("assembly_point_name")]
        public string? AssemblyPointName { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }
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

        [JsonPropertyName("sos_request_id")]
        public int? SosRequestId { get; set; }

        [JsonPropertyName("depot_id")]
        public int? DepotId { get; set; }

        [JsonPropertyName("depot_name")]
        public string? DepotName { get; set; }

        [JsonPropertyName("depot_address")]
        public string? DepotAddress { get; set; }

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
