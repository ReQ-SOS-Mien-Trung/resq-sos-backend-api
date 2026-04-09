using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Enum.Personnel;
using RESQ.Domain.Enum.System;

namespace RESQ.Infrastructure.Services;

public class RescueMissionSuggestionService : IRescueMissionSuggestionService
{
    private readonly IAiProviderClientFactory _aiProviderClientFactory;
    private readonly IAiPromptExecutionSettingsResolver _settingsResolver;
    private readonly IPromptRepository _promptRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository;
    private readonly IAssemblyPointRepository _assemblyPointRepository;
    private readonly ILogger<RescueMissionSuggestionService> _logger;

    // Fallback defaults
    private const string FallbackModel = "gemini-2.5-flash";
    private const string FallbackApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
    private const double FallbackTemperature = 0.5;
    private const int FallbackMaxTokens = 65535;

    // Agent loop constants
    private const int MaxAgentTurns = 20;
    private const int AgentPageSize = 10;
    private const string CollectSuppliesActivityType = "COLLECT_SUPPLIES";
    private const string ReturnSuppliesActivityType = "RETURN_SUPPLIES";
    private const string ReusableItemType = "Reusable";
    private const string SingleTeamExecutionMode = "SingleTeam";
    private static readonly string[] OnSiteActivityTypes = ["DELIVER_SUPPLIES", "RESCUE", "MEDICAL_AID", "EVACUATE"];
    private static readonly Regex SosIdRegex = new(@"SOS\s*ID\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CoordinateRegex = new(@"(-?\d{1,3}\.\d+)\s*,\s*(-?\d{1,3}\.\d+)", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public RescueMissionSuggestionService(
        IAiProviderClientFactory aiProviderClientFactory,
        IAiPromptExecutionSettingsResolver settingsResolver,
        IPromptRepository promptRepository,
        IDepotInventoryRepository depotInventoryRepository,
        IItemModelMetadataRepository itemModelMetadataRepository,
        IAssemblyPointRepository assemblyPointRepository,
        ILogger<RescueMissionSuggestionService> logger)
    {
        _aiProviderClientFactory = aiProviderClientFactory;
        _settingsResolver = settingsResolver;
        _promptRepository = promptRepository;
        _depotInventoryRepository = depotInventoryRepository;
        _itemModelMetadataRepository = itemModelMetadataRepository;
        _assemblyPointRepository = assemblyPointRepository;
        _logger = logger;
    }

    public async Task<RescueMissionSuggestionResult> GenerateSuggestionAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots = null,
        List<AgentTeamInfo>? nearbyTeams = null,
        bool isMultiDepotRecommended = false,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        RescueMissionSuggestionResult? finalResult = null;

        try
        {
            await foreach (var evt in GenerateSuggestionStreamAsync(
                sosRequests, nearbyDepots, nearbyTeams, isMultiDepotRecommended, cancellationToken))
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
            ghi_chu_su_co_moi_nhat = sos.LatestIncidentNote,
            lich_su_su_co = sos.IncidentNotes,
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

              **Lập lịch trình theo tối ưu địa lý (quan trọng)**:
              - Sắp xếp thứ tự các bước dựa trên tọa độ thực tế để **tổng quãng đường di chuyển là ngắn nhất**.
              - Nếu trên đường từ Kho A đến Kho B có điểm SOS mà Kho A đã có đủ vật tư để phục vụ → **được phép** dừng xử lý SOS đó trước rồi mới đến Kho B.
                Ví dụ hợp lệ: COLLECT(Kho A) → DELIVER/RESCUE SOS #1 (tiện đường) → COLLECT(Kho B) → DELIVER/RESCUE SOS #2
              - Nếu tất cả các kho nằm cùng một phía và các điểm SOS nằm phía khác → ưu tiên gom hàng từ tất cả kho trước, rồi mới đến các điểm SOS.
                Ví dụ hợp lệ: COLLECT(Kho A) → COLLECT(Kho B) → DELIVER SOS #1 → RESCUE SOS #2
              - **CẤM**: Quay ngược lại kho khi không có lý do địa lý — tức là đã đến điểm SOS xong lại quay ngược hướng về kho lấy thêm đồ cho CHÍNH SOS ĐÓ hoặc cho SOS đã xử lý xong.
              - Ghi rõ lý do địa lý trong `description` của từng bước (ví dụ: "tiện đường từ Kho A sang Kho B", "Kho A đã có đủ vật tư cho SOS #1").

              **Lưu ý khi xuất JSON**:
              - Ưu tiên dùng nhiều kho khác nhau nếu dữ liệu searchInventory trả về vật tư từ nhiều kho. Nếu chỉ tìm thấy 1 kho có vật tư, thì 1 kho là chấp nhận được — ghi chú trong `special_notes`: "Chỉ tìm thấy vật tư tại 1 kho trong khu vực".

              """
            : string.Empty;

        return $$"""
            ## HƯỚNG DẪN SỬ DỤNG CÔNG CỤ
            Bạn có thể gọi ba công cụ để tìm kiếm dữ liệu thực từ hệ thống trước khi lập kế hoạch:

            - **searchInventory(category, type?, page)**: Tìm kiếm vật tư trong kho theo danh mục (ví dụ: "Nước", "Thực phẩm", "Y tế", "Cứu hộ"). Trả về danh sách vật tư khả dụng kèm theo item_id, tên, item_type, **available_quantity** (số lượng tiêu hao khả dụng hoặc số đơn vị reusable đang Available), depot_id, tên kho, địa chỉ kho, depot_latitude, depot_longitude. Với vật tư reusable còn có thêm `good_available_count`, `fair_available_count`, `poor_available_count`. Mỗi hàng trong kết quả là một (vật tư, kho) riêng biệt — cùng một vật tư có thể xuất hiện ở nhiều hàng nếu có ở nhiều kho khác nhau.
            - **getTeams(ability?, available?, page)**: Tìm kiếm đội cứu hộ trong pool nearby teams của cluster hiện tại. Có thể lọc theo khả năng (team_type). Trả về team_id, tên, loại, trạng thái, số thành viên, vị trí điểm tập kết (assembly_point_name, latitude, longitude) và distance_km.
            - **getAssemblyPoints(page)**: Lấy danh sách điểm tập kết đang hoạt động. Trả về assembly_point_id, tên, sức chứa tối đa và vị trí (latitude, longitude).

            **BẮT BUỘC trước khi lập kế hoạch**:
            - Gọi **searchInventory** cho TỪNG danh mục: Thực phẩm, Nước, Y tế, Cứu hộ (và các danh mục khác phù hợp). Không bỏ qua danh mục nào có thể liên quan.
            - Gọi **getTeams** để lấy team_id cho `suggested_team`.
            - Nếu có activity loại **RESCUE** hoặc **EVACUATE**, bắt buộc gọi **getAssemblyPoints** và chọn `assembly_point_id` gần nạn nhân nhất cho từng activity đó.
            - Dùng đúng item_id, depot_id, team_id từ kết quả — KHÔNG tự tạo ID.
            {{multiDepotSection}}
            ## QUY TẮC KIỂM TRA VÀ BÁO CÁO THIẾU HỤT VẬT TƯ (BẮT BUỘC)
            Sau khi nhận kết quả searchInventory, kết quả có thể chứa cùng một loại vật tư từ **nhiều kho khác nhau**.
            Mỗi dòng kết quả là một cặp (vật tư, kho) — PHẢI đọc và tổng hợp từ tất cả các dòng trước khi quyết định.

            **Bước 1 — Gom hàng từ nhiều kho nếu một kho không đủ**:
            - Với mỗi loại vật tư cần, so sánh `needed_quantity` với `available_quantity` của từng kho xuất hiện trong kết quả.
            - Nếu Kho A không đủ: lấy hết Kho A, rồi bổ sung từ Kho B, Kho C... cho đến đủ hoặc hết nguồn cung.
            - Mỗi kho đóng góp vào bước COLLECT_SUPPLIES riêng của kho đó (không gộp chung).
            - Ví dụ: Cần 500 chai nước. Kho A có 300, Kho B có 250.
              → COLLECT(Kho A): 300 chai nước + COLLECT(Kho B): 200 chai nước → tổng = 500 ✓
            - Ghi rõ trong description từng COLLECT: "Lấy [vật tư] x[qty] để bù đắp số lượng thiếu từ [Kho A]".

            **Bước 2 — Xác định tình trạng sau khi đã gom từ tất cả kho**:

            **Trường hợp 1 — Đủ hàng** (`tổng available_quantity từ tất cả kho >= needed_quantity`):
            - Tạo các bước COLLECT_SUPPLIES riêng biệt cho từng kho đóng góp.
            - Tổng `supplies_to_collect.quantity` trên tất cả các bước COLLECT = đúng `needed_quantity`.

            **Trường hợp 2 — Thiếu một phần** (`0 < tổng available_quantity từ TẤT CẢ kho < needed_quantity`):
            - Lấy hết tất cả những gì có thể từ tất cả kho (mỗi kho một bước COLLECT riêng).
            - BẮT BUỘC ghi vào `special_notes`:
              `"[SOS ID X]: Thiếu [TÊN VẬT TƯ] x[SỐ LƯỢNG THIẾU] [đơn vị] (tổng tất cả kho chỉ có [tổng_available]/[needed_quantity] [đơn vị])"`

            **Trường hợp 3 — Không có trong kho** (không tìm thấy vật tư trong bất kỳ kho nào):
            - KHÔNG tạo bước COLLECT_SUPPLIES cho vật tư này.
            - BẮT BUỘC ghi vào `special_notes`: `"[SOS ID X]: Không có [TÊN VẬT TƯ] trong hệ thống kho"`
            - **PHÂN BIỆT RÕ**: "Không có trong kho" khác với "thiếu một phần" — KHÔNG viết "kho chỉ có 0/X" cho trường hợp này.

            ## QUY TẮC THỨ TỰ ACTIVITIES — TỐI ƯU ĐỊA LÝ
            Thứ tự các bước phải được tối ưu dựa trên **tọa độ thực tế** để quãng đường di chuyển tổng cộng là ngắn nhất.

            **Nguyên tắc lập lịch**:
            - Sắp xếp các bước theo trình tự di chuyển hợp lý trên bản đồ — không được tạo ra hành trình "đi rồi quay ngược lại" vô lý.
            - Được phép xen kẽ COLLECT và hoạt động SOS nếu địa lý cho phép:
              - ĐÚNG: COLLECT(Kho A) → DELIVER SOS #1 (tiện đường) → COLLECT(Kho B) → RESCUE SOS #2
              - ĐÚNG: COLLECT(Kho A) → COLLECT(Kho B) → DELIVER SOS #1 → RESCUE SOS #2
              - SAI:  COLLECT(Kho A) → DELIVER SOS #1 → quay ngược về Kho A để lấy thêm đồ cho SOS #1 đã phục vụ xong
              - SAI:  COLLECT(Kho A) → RESCUE SOS #2 (xa kho B) → COLLECT(Kho B ở hướng ngược lại) → quay về SOS #2 giao thêm hàng

            **Quy tắc bắt buộc**:
            - COLLECT_SUPPLIES phải đứng TRƯỚC hoạt động (DELIVER/RESCUE/MEDICAL_AID) sử dụng vật tư từ kho đó.
            - KHÔNG được tạo COLLECT_SUPPLIES sau khi đã DELIVER/RESCUE cho SOS đó xong (không được quay lại kho để bổ sung cho SOS đã hoàn tất).
            - Nếu cần nhiều kho, sắp xếp thứ tự kho theo địa lý (đi lần lượt, không vòng vèo).
            - Ghi rõ lý do địa lý trong `description` khi có bước xen kẽ giữa COLLECT và SOS.
            - Nếu bất kỳ bước `COLLECT_SUPPLIES` nào lấy vật tư có `item_type = Reusable`, bạn **phải** tạo bước `RETURN_SUPPLIES` ở cuối kế hoạch để đưa đúng số vật tư reusable đó về lại đúng kho nguồn.
            - Mỗi cặp (`depot_id`, `suggested_team.team_id`) phải có `RETURN_SUPPLIES` riêng. Không gộp nhiều kho hoặc nhiều đội vào cùng một bước trả.

            ## QUY TẮC MỖI SOS CHỈ XỬ LÝ TẠI HIỆN TRƯỜNG MỘT ĐỢT
            - Khi một SOS cần vật tư từ nhiều kho, bạn phải lập kế hoạch **lấy đủ toàn bộ vật tư cần thiết từ tất cả các kho trước**, rồi mới tới hiện trường SOS đó.
            - Sau khi đã bắt đầu hoạt động tại hiện trường của một SOS (`DELIVER_SUPPLIES`, `RESCUE`, `MEDICAL_AID`, `EVACUATE`), **không được** chèn thêm bất kỳ bước `COLLECT_SUPPLIES` nào cho chính SOS đó ở phía sau.
            - Với mỗi SOS, các bước tại hiện trường phải được gom thành một cụm liên tiếp, ví dụ hợp lệ: `COLLECT(Kho A)` → `COLLECT(Kho B)` → `DELIVER SOS #2` → `RESCUE SOS #2` → `MEDICAL_AID SOS #2`.
            - Ví dụ sai: `COLLECT(Kho A)` → `DELIVER SOS #2` → `RESCUE SOS #2` → `COLLECT(Kho B)` → `DELIVER SOS #2`.
            - Nếu không thể lấy đủ vật tư, vẫn phải lấy hết tất cả vật tư hiện có trước khi đến hiện trường lần đầu, sau đó ghi thiếu hụt vào `special_notes` thay vì lập kế hoạch quay lại kho.
            - **KHÔNG được để bước `COLLECT_SUPPLIES` ở cuối kế hoạch nếu phía sau không còn bước nào sử dụng số vật tư đó.** Mọi bước COLLECT phải đứng trước cụm activity hiện trường tương ứng.
            - **KHÔNG được gộp nhiều SOS vào cùng một bước `EVACUATE`.** Mỗi SOS cần sơ tán phải có bước `EVACUATE` riêng ngay sau khi xử lý xong SOS đó, rồi mới được di chuyển sang SOS tiếp theo.
            - Ví dụ đúng: `COLLECT` → `DELIVER SOS #2` → `RESCUE SOS #2` → `MEDICAL_AID SOS #2` → `EVACUATE SOS #2` → `DELIVER SOS #1` → `RESCUE SOS #1` → `EVACUATE SOS #1`.
            - Ví dụ sai: `DELIVER SOS #2` → `RESCUE SOS #2` → `DELIVER SOS #1` → `RESCUE SOS #1` → `EVACUATE SOS #2 và SOS #1`.

            ## QUY TẮC CHO TỪNG LOẠI ACTIVITY

            ### COLLECT_SUPPLIES
            - Chỉ tạo khi có vật tư thực tế trong kho (available_quantity > 0).
            - `depot_id`, `depot_name`, `depot_address` phải khớp với kho thực tế trả về từ searchInventory.
            - `depot_latitude`, `depot_longitude` phải điền theo `depot_latitude`, `depot_longitude` trả về từ searchInventory (để frontend hiển thị bản đồ).
            - `supplies_to_collect` chỉ chứa vật tư lấy từ kho đó với số lượng thực tế lấy.

            ### DELIVER_SUPPLIES
            - Tạo sau mỗi COLLECT_SUPPLIES để giao hàng đến điểm sự cố.
            - `supplies_to_collect` liệt kê đúng những gì đã lấy từ bước COLLECT tương ứng.

            ### RETURN_SUPPLIES
            - Chỉ dùng để trả vật tư tái sử dụng (`item_type = Reusable`) về kho nguồn sau khi hoàn tất các bước hiện trường.
            - `RETURN_SUPPLIES` phải nằm ở cuối kế hoạch cho đúng cặp đội + kho đã lấy vật tư reusable.
            - `depot_id`, `depot_name`, `depot_address`, `depot_latitude`, `depot_longitude` phải khớp kho gốc; `supplies_to_collect` chỉ chứa các vật tư reusable thực sự cần trả.
            - Không đưa vật tư consumable vào `RETURN_SUPPLIES`.

            ### RESCUE
            - **LUÔN tạo bước RESCUE** ngay cả khi thiếu thiết bị cứu hộ.
            - Nếu cần thiết bị cứu hộ chuyên dụng (dụng cụ phá dỡ, thiết bị nâng đỡ v.v.) và có trong kho → đưa vào `supplies_to_collect`.
            - Nếu thiết bị cần thiết KHÔNG có trong kho → ghi rõ vào `special_notes` rằng thiếu thiết bị nào. KHÔNG tạo thêm bước nào khác cho trường hợp này.
            - Với mỗi bước RESCUE, phải điền `assembly_point_id` bằng điểm tập kết đang hoạt động gần vị trí nạn nhân nhất từ kết quả `getAssemblyPoints`.

            ### MEDICAL_AID
            - **LUÔN có `supplies_to_collect`** nếu tình huống cần vật tư y tế (sơ cứu, thuốc, dụng cụ y tế).
            - Trước khi tạo bước MEDICAL_AID, PHẢI gọi searchInventory với danh mục "Y tế" để lấy danh sách vật tư y tế khả dụng.
            - Nếu có vật tư y tế trong kho → điền vào `supplies_to_collect` với item_id và depot_id thực tế.
            - Nếu vật tư y tế KHÔNG có hoặc THIẾU → vẫn tạo bước MEDICAL_AID, để `supplies_to_collect: null`, và ghi vào `special_notes` rằng thiếu vật tư y tế cụ thể nào.
            - Đừng bỏ qua bước COLLECT_SUPPLIES cho vật tư y tế nếu có trong kho — phải lấy trước khi thực hiện MEDICAL_AID.

            ### EVACUATE
            - Tạo khi cần vận chuyển người bị thương đến cơ sở y tế.
            - `supplies_to_collect`: null (không lấy vật tư ở bước này).
            - Với mỗi bước EVACUATE, phải điền `assembly_point_id` bằng điểm tập kết đang hoạt động gần vị trí nạn nhân nhất từ kết quả `getAssemblyPoints`.

            **QUY TẮC RETRY khi tìm đội (rất quan trọng)**:
            - Luôn thử `getTeams(page=1)` hoặc `getTeams(ability=..., page=1)` trước để lấy đội gần nhất trong vùng.
            - Nếu bạn có dùng lọc `ability` mà không thấy đội nào, gọi lại `getTeams` **không truyền `ability`** để lấy toàn bộ nearby team pool.
            - Công cụ `getTeams` **chỉ** trả về các đội đang Available và nằm trong bán kính cluster hiện tại. Truyền `available=false` **không** được hiểu là mở rộng ra đội xa hơn hoặc đội không Available.
            - Nếu `getTeams` vẫn rỗng sau khi bỏ ability filter, lúc đó mới được để `suggested_team = null` và phải ghi rõ trong `coordination_notes` hoặc `reason` rằng cluster hiện không có nearby team phù hợp.
            - KHÔNG được tự suy diễn hoặc tạo ra team ngoài kết quả thực tế của `getTeams`.

            ## SỬ DỤNG VỊ TRÍ ĐỂ LẬP KẾ HOẠCH
            Mỗi SOS request có trường `vi_tri` chứa tọa độ (latitude, longitude) của sự cố.
            Kết quả searchInventory trả về `depot_latitude`, `depot_longitude` — tọa độ của kho vật tư.
            Kết quả getTeams trả về `latitude`, `longitude` — tọa độ điểm tập kết của đội cứu hộ.

            **Quy tắc sử dụng vị trí**:
            - Ưu tiên chọn kho vật tư **gần nhất** với vị trí sự cố (so sánh tọa độ).
            - Ưu tiên chọn đội cứu hộ có điểm tập kết **gần nhất** với vị trí sự cố.
            - Với activity RESCUE hoặc EVACUATE, ưu tiên chọn **assembly point gần nhất** với vị trí sự cố và điền `assembly_point_id` vào activity.
            - Khi có nhiều sự cố, phân công đội và kho sao cho quãng đường di chuyển tổng cộng là nhỏ nhất.
            - Ghi rõ lý do chọn kho và đội dựa trên vị trí địa lý trong trường `reason` và `description`.

            **Trường bắt buộc trong suggested_team**: Ngoài team_id, team_name, team_type và reason, bạn **phải** điền thêm:
            - `assembly_point_name`: tên điểm tập kết (lấy từ kết quả getTeams)
            - `latitude`: vĩ độ điểm tập kết (lấy từ kết quả getTeams)
            - `longitude`: kinh độ điểm tập kết (lấy từ kết quả getTeams)
                        - `distance_km`: khoảng cách từ điểm tập kết đến cluster (lấy từ kết quả getTeams nếu có)
            Nếu đội không có điểm tập kết (giá trị null trong kết quả), hãy để các trường đó là null.

                        ## QUY TẮC PHÂN TÍCH MỘT ĐỘI HAY NHIỀU ĐỘI
                        - Mỗi activity **bắt buộc** phải có các trường sau:
                            - `execution_mode`: chỉ được là `SingleTeam` hoặc `SplitAcrossTeams`
                            - `required_team_count`: số đội tối thiểu cần cho mục tiêu thực tế của activity này
                            - `coordination_group_key`: để `null` nếu `execution_mode = SingleTeam`; bắt buộc có nếu `execution_mode = SplitAcrossTeams`
                            - `coordination_notes`: giải thích rõ vì sao activity này do một đội tự làm được hoặc là một phần của kế hoạch nhiều đội
                        - `SingleTeam` nghĩa là một đội có thể hoàn thành mục tiêu của activity này một cách độc lập.
                        - `SplitAcrossTeams` nghĩa là mục tiêu thực tế cần nhiều đội, nhưng **mỗi activity trong JSON vẫn chỉ đại diện cho phần việc của đúng một đội**.
                        - Backend hiện chỉ thực thi theo mô hình **một activity - một team**. Vì vậy nếu cần nhiều đội, bạn **phải tách thành nhiều activity riêng**, mỗi activity có `suggested_team` riêng nhưng dùng cùng `coordination_group_key`.
                        - Ví dụ đúng: Team A đi lấy nước và y tế ở Kho X, Team B đi lấy dụng cụ cứu hộ ở chính Kho X. Khi đó phải tạo **hai** `COLLECT_SUPPLIES` khác nhau, cùng `depot_id = X`, nhưng khác `supplies_to_collect`, khác `suggested_team`, cùng `coordination_group_key`, và `coordination_notes` phải nói rõ đây là kế hoạch chia vật tư theo đội.
                        - Ví dụ sai: chỉ tạo một `COLLECT_SUPPLIES` tại Kho X rồi ghi trong mô tả rằng cả Team A và Team B cùng làm activity đó.

            ## QUY TẮC PHÂN CÔNG ĐỘI VÀO ACTIVITY
            - **MỖI activity PHẢI có trường `suggested_team`** — không được để null trừ khi thực sự không tìm được đội nào.
            - Sau khi gọi getTeams, phân công đội phù hợp vào từng activity dựa trên loại hoạt động và vị trí.
            - Nếu một đội đảm nhận nhiều activity, điền cùng một đội vào `suggested_team` của từng activity đó.
                        - Nếu một mục tiêu thực tế cần nhiều đội, tách thành nhiều activity đơn-team. Mỗi activity vẫn chỉ có **một** `suggested_team`, nhưng `execution_mode` phải là `SplitAcrossTeams` và các activity liên quan phải chia sẻ cùng `coordination_group_key`.
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
                                "longitude": 106.62,
                                "distance_km": 3.2
              }
              ```
                        - Format tối thiểu cho phần phân tích execution trong mỗi activity:
                            ```json
                            "execution_mode": "SplitAcrossTeams",
                            "required_team_count": 2,
                            "coordination_group_key": "collect-depot-12-sos-45",
                            "coordination_notes": "Kho này được chia cho 2 đội; activity này là phần lấy vật tư của Team A."
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
                AssemblyPointName  = parsed.SuggestedTeam.AssemblyPointName,
                Latitude           = parsed.SuggestedTeam.Latitude,
                Longitude          = parsed.SuggestedTeam.Longitude,
                DistanceKm         = parsed.SuggestedTeam.DistanceKm
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
            if (st.TryGetProperty("assembly_point_name",out var apn) && apn.ValueKind != JsonValueKind.Null)                                        teamDto.AssemblyPointName = apn.GetString();
            if (st.TryGetProperty("latitude",           out var lat) && lat.ValueKind != JsonValueKind.Null && lat.TryGetDouble(out var latv))       teamDto.Latitude          = latv;
            if (st.TryGetProperty("longitude",          out var lon) && lon.ValueKind != JsonValueKind.Null && lon.TryGetDouble(out var lonv))       teamDto.Longitude         = lonv;
            if (st.TryGetProperty("distance_km",        out var dkm) && dkm.ValueKind != JsonValueKind.Null && dkm.TryGetDouble(out var dkmv))       teamDto.DistanceKm        = dkmv;
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

    private static bool IsCollectActivity(SuggestedActivityDto activity) =>
        string.Equals(activity.ActivityType, CollectSuppliesActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsReturnActivity(SuggestedActivityDto activity) =>
        string.Equals(activity.ActivityType, ReturnSuppliesActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsOnSiteActivity(SuggestedActivityDto activity) =>
        OnSiteActivityTypes.Contains(activity.ActivityType ?? string.Empty, StringComparer.OrdinalIgnoreCase);

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
                AssemblyPointName = activity.SuggestedTeam.AssemblyPointName,
                Latitude = activity.SuggestedTeam.Latitude,
                Longitude = activity.SuggestedTeam.Longitude,
                DistanceKm = activity.SuggestedTeam.DistanceKm
            }
        };
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
        activity.CoordinationNotes = "Một đội trả vật tư tái sử dụng đã lấy trước đó về lại kho nguồn.";
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
            ? $"Hoàn tất nhiệm vụ, đưa vật tư tái sử dụng về lại {depotLabel}."
            : $"Hoàn tất nhiệm vụ, đưa vật tư tái sử dụng về lại {depotLabel}. Trả: {itemSummary}.";
    }

    private static SupplyToCollectDto CloneSupply(SupplyToCollectDto supply)
    {
        return new SupplyToCollectDto
        {
            ItemId = supply.ItemId,
            ItemName = supply.ItemName,
            Quantity = supply.Quantity,
            Unit = supply.Unit
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
            .Where(a => a.Status == AssemblyPointStatus.Active && a.Location is not null)
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

    private static string NormalizeItemName(string name) =>
        name.ToLowerInvariant()
            .Replace("&", " ")
            .Replace("(", " ")
            .Replace(")", " ")
            .Replace(",", " ")
            .Replace("-", " ")
            .Replace("/", " ")
            .Replace("  ", " ")
            .Trim();

    /// <summary>
    /// Populates DestinationLatitude / DestinationLongitude / DestinationName on each activity
    /// from structured context data (depots and SOS requests), then cleans up descriptions so
    /// that named destinations are shown by name rather than raw coordinates.
    /// Falls back to a DB lookup when the depot picked by the AI is not in the nearbyDepots list
    /// (the AI uses searchInventory which covers all depots, not only the pre-filtered nearby set).
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
            // than raw coordinate pairs — coordinates are still available on the DTO fields.
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
        new(@"\(?\s*(-?\d{1,3}\.\d+)\s*[,，]\s*(-?\d{1,3}\.\d+)\s*\)?", RegexOptions.Compiled);

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

    // ─── SSE helpers ───────────────────────────────────────────────────────────

    private static SseMissionEvent Status(string msg) =>
        new() { EventType = "status", Data = msg };

    private static SseMissionEvent Error(string msg) =>
        new() { EventType = "error", Data = msg };

    // ─── Streaming (SSE agent loop) ────────────────────────────────────────────

    public async IAsyncEnumerable<SseMissionEvent> GenerateSuggestionStreamAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots = null,
        List<AgentTeamInfo>? nearbyTeams = null,
        bool isMultiDepotRecommended = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var availableNearbyTeams = nearbyTeams ?? [];

        yield return Status("Đang tải cấu hình AI agent...");

        var prompt = await _promptRepository.GetActiveByTypeAsync(PromptType.MissionPlanning, cancellationToken);
        if (prompt == null)
        {
            yield return Error("Chưa có prompt 'MissionPlanning' đang được kích hoạt. Vui lòng cấu hình trong quản trị hệ thống.");
            yield break;
        }

        var settings = _settingsResolver.Resolve(
            prompt,
            new AiPromptExecutionFallback(
                FallbackModel,
                FallbackApiUrl,
                FallbackTemperature,
                FallbackMaxTokens));

        // Enforce minimum 32K tokens — mission plans with tool calls can be very long
        var maxTokens = Math.Max(settings.MaxTokens, 32768);

        // Build the initial user message (no pre-loaded depot data; agent fetches via tools)
        var sosDataJson = BuildSosRequestsData(sosRequests);
        var userMessage = (prompt.UserPromptTemplate ?? string.Empty)
            .Replace("{{sos_requests_data}}", sosDataJson)
            .Replace("{{total_count}}", sosRequests.Count.ToString())
            .Replace("{{depots_data}}", "(Dữ liệu kho không được truyền trực tiếp. Hãy gọi công cụ searchInventory để tra cứu vật tư khả dụng theo từng danh mục, rồi dùng dữ liệu đó để lập bước COLLECT_SUPPLIES và DELIVER_SUPPLIES.)")
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
                    toolResult = await ExecuteToolAsync(toolCall.Name, toolCall.Arguments, availableNearbyTeams, cancellationToken);
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
        var sosLookup = sosRequests.ToDictionary(x => x.Id);
        NormalizeActivitySequence(result.SuggestedActivities, sosLookup);
        BackfillItemIds(result.SuggestedActivities, nearbyDepots ?? []);
        BackfillSosRequestIds(result.SuggestedActivities, sosRequests);
        await EnrichActivitiesWithAssemblyPointsAsync(result, sosLookup, cancellationToken);
        await EnsureReusableReturnActivitiesAsync(result.SuggestedActivities, cancellationToken);
        await BackfillDestinationInfoAsync(result.SuggestedActivities, nearbyDepots ?? [], sosRequests, cancellationToken);
        result.IsSuccess     = true;
        result.ModelName     = settings.Model;
        result.RawAiResponse = finalText;

        _logger.LogInformation(
            "Agent mission suggestion: Provider={provider}, Model={model}, Title={title}, Type={type}, Activities={count}, Team={team}, Confidence={conf}",
            settings.Provider, settings.Model,
            result.SuggestedMissionTitle, result.SuggestedMissionType,
            result.SuggestedActivities.Count,
            result.SuggestedTeam?.TeamName ?? "none",
            result.ConfidenceScore);

        yield return new SseMissionEvent { EventType = "result", Result = result };
    }

    // ─── Tool execution ────────────────────────────────────────────────────────

    private async Task<JsonElement> ExecuteToolAsync(
        string toolName,
        JsonElement args,
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
                    .Where(a => a.Status == AssemblyPointStatus.Active)
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

                var total = assemblyPoints.Count(a => a.Status == AssemblyPointStatus.Active);
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
            Description = "Tìm kiếm vật tư đang khả dụng trong kho theo danh mục và loại. Trả về cả consumable lẫn reusable với item_id, tên, item_type, available_quantity, kho chứa và tọa độ vị trí kho (depot_latitude, depot_longitude). Reusable còn có good_available_count, fair_available_count, poor_available_count.",
            Parameters = ParseJson(
                """
                {
                  "type": "object",
                  "properties": {
                    "category": { "type": "string", "description": "Tên danh mục vật tư, ví dụ: 'Nước', 'Thực phẩm', 'Y tế', 'Quần áo'" },
                    "type": { "type": "string", "description": "Tên loại hoặc tên vật tư cụ thể trong danh mục (tuỳ chọn)" },
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
