using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Enum.Personnel;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Options;

namespace RESQ.Infrastructure.Services;

public partial class RescueMissionSuggestionService : IRescueMissionSuggestionService
{
    private readonly IAiProviderClientFactory _aiProviderClientFactory;
    private readonly IAiPromptExecutionSettingsResolver _settingsResolver;
    private readonly IPromptRepository _promptRepository;
    private readonly IMissionAiSuggestionRepository _missionAiSuggestionRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository;
    private readonly IAssemblyPointRepository _assemblyPointRepository;
    private readonly MissionSuggestionPipelineOptions _pipelineOptions;
    private readonly ILogger<RescueMissionSuggestionService> _logger;

    private const string FallbackModel = "gemini-2.5-flash";
    private const string FallbackApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
    private const double FallbackTemperature = 0.5;
    private const int FallbackMaxTokens = 65535;
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
        IMissionAiSuggestionRepository missionAiSuggestionRepository,
        IDepotInventoryRepository depotInventoryRepository,
        IItemModelMetadataRepository itemModelMetadataRepository,
        IAssemblyPointRepository assemblyPointRepository,
        IOptions<MissionSuggestionPipelineOptions> pipelineOptions,
        ILogger<RescueMissionSuggestionService> logger)
    {
        _aiProviderClientFactory = aiProviderClientFactory;
        _settingsResolver = settingsResolver;
        _promptRepository = promptRepository;
        _missionAiSuggestionRepository = missionAiSuggestionRepository;
        _depotInventoryRepository = depotInventoryRepository;
        _itemModelMetadataRepository = itemModelMetadataRepository;
        _assemblyPointRepository = assemblyPointRepository;
        _pipelineOptions = pipelineOptions.Value;
        _logger = logger;
    }

    public async Task<RescueMissionSuggestionResult> GenerateSuggestionAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots = null,
        List<AgentTeamInfo>? nearbyTeams = null,
        bool isMultiDepotRecommended = false,
        int? clusterId = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        RescueMissionSuggestionResult? finalResult = null;

        try
        {
            await foreach (var evt in GenerateSuggestionStreamAsync(
                sosRequests, nearbyDepots, nearbyTeams, isMultiDepotRecommended, clusterId, cancellationToken))
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
                ErrorMessage = $"L?i khi g?i AI: {ex.Message}",
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
            ErrorMessage = "AI không ph?n h?i. Vui lòng th? l?i sau.",
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
            loai_sos = sos.SosType ?? "Không xác d?nh",
            tin_nhan = sos.RawMessage,
            du_lieu_chi_tiet = sos.StructuredData ?? "Không có",
            muc_uu_tien = sos.PriorityLevel ?? "Chua dánh giá",
            trang_thai = sos.Status ?? "Không rõ",
            ghi_chu_su_co_moi_nhat = sos.LatestIncidentNote,
            lich_su_su_co = sos.IncidentNotes,
            vi_tri = sos.Latitude.HasValue && sos.Longitude.HasValue
                ? $"{sos.Latitude}, {sos.Longitude}"
                : "Không xác d?nh",
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
        _ = isMultiDepotRecommended;

        return """
            ## HU?NG D?N S? D?NG CÔNG C?
            B?n có th? g?i ba công c? d? l?y d? li?u th?c tru?c khi l?p k? ho?ch:

            - **searchInventory(category, type?, page)**: Tìm v?t ph?m kh? d?ng trong **các kho h?p l? c?a cluster hi?n t?i**. K?t qu? ch? ch?a các kho backend dã cho phép trong ph?m vi l?p k? ho?ch này. M?i dòng là m?t c?p (v?t ph?m, kho) v?i item_id, item_name, item_type, available_quantity, depot_id, depot_name, depot_address, depot_latitude, depot_longitude.
            - **getTeams(ability?, available?, page)**: Tr? v? nearby teams dang Available trong bán kính cluster hi?n t?i.
            - **getAssemblyPoints(page)**: Tr? v? các assembly point dang ho?t d?ng.

            ## QUY T?C KHO — CH? CH?N M?T KHO CHO TOÀN B? MISSION
            - B?T BU?C g?i **searchInventory** cho t?ng danh m?c phù h?p: Th?c ph?m, Nu?c, Y t?, C?u h?, Qu?n áo, noi trú ?n... Không b? sót danh m?c liên quan.
            - Sau khi có k?t qu?, so sánh các `depot_id` xu?t hi?n và ch?n **dúng m?t kho phù h?p nh?t cho toàn b? mission**.
            - Tiêu chí ch?n kho: uu tiên kho dáp ?ng du?c nhi?u nhu c?u SOS nh?t và có t?ng s? lu?ng phù h?p cao nh?t. N?u tuong duong, ch?n kho có v? trí thu?n l?i hon trong k?t qu? dã tr? v?.
            - Toàn b? activity có dùng kho trong mission này ph?i dùng cùng m?t `depot_id`, `depot_name`, `depot_address` c?a kho dã ch?n.
            - **TUY?T Ð?I KHÔNG** t?o k? ho?ch l?y v?t ph?m t? kho th? hai, không chia v?t ph?m gi?a nhi?u kho, không g?p nhi?u kho.
            - N?u kho dã ch?n không d? d?, v?n ch? l?y nh?ng gì kho dó hi?n có r?i báo thi?u. Không du?c chuy?n sang kho khác.

            ## BÁO CÁO THI?U H?T v?t ph?m
            - N?u sau khi d?i chi?u v?i kho dã ch?n mà còn thi?u b?t k? v?t ph?m nào, d?t `needs_additional_depot = true`.
            - Khi có thi?u h?t, di?n `supply_shortages` v?i t?ng dòng thi?u theo format:
              - `sos_request_id`: SOS b? ?nh hu?ng
              - `item_id`: n?u xác d?nh du?c t? inventory; n?u không thì d? null
              - `item_name`, `unit`
              - `selected_depot_id`, `selected_depot_name`: chính là kho duy nh?t dã ch?n
              - `needed_quantity`, `available_quantity`, `missing_quantity`
              - `notes`: mô t? ng?n g?n lý do thi?u n?u c?n
            - N?u kho dã ch?n không có món dó, dùng `available_quantity = 0` và `missing_quantity = needed_quantity`.
            - N?u kho ch? có m?t ph?n, dùng `available_quantity < needed_quantity` và `missing_quantity = needed_quantity - available_quantity`.
            - `special_notes` ph?i ghi rõ r?ng coordinator c?n b? sung thêm kho/ngu?n c?p phát vì dang thi?u v?t ph?m nào và s? lu?ng thi?u bao nhiêu.
            - N?u không có thi?u h?t, d?t `needs_additional_depot = false` và `supply_shortages = []`.

            ## QUY T?C ESTIMATE TIME
            - M?i activity ph?i có `estimated_time` theo dúng m?t trong hai format: `"X phút"` ho?c `"Y gi? Z phút"`.
            - `estimated_time` ph?i bao g?m th?i gian di chuy?n th?c d?a + th?i gian l?y hàng/giao hàng + th?i gian x? lý t?i hi?n tru?ng tuong ?ng v?i activity dó.
            - `estimated_duration` là t?ng th?i gian tu?n t? c?a toàn b? activities theo dúng th? t? step trong mission, cung dùng format `"X phút"` ho?c `"Y gi? Z phút"`.
            - Không d? `estimated_time` ho?c `estimated_duration` mo h? ki?u `"nhanh"`, `"s?m"`, `"kho?ng vài gi?"`.

            ## QUY T?C TH? T? ACTIVITY
            - `COLLECT_SUPPLIES` ph?i d?ng tru?c activity hi?n tru?ng s? d?ng s? v?t ph?m dó.
            - Không du?c t?o thêm `COLLECT_SUPPLIES` cho cùng SOS sau khi dã b?t d?u `DELIVER_SUPPLIES`, `RESCUE`, `MEDICAL_AID`, ho?c `EVACUATE` c?a SOS dó.
            - N?u có v?t ph?m reusable du?c l?y ? `COLLECT_SUPPLIES`, ph?i có `RETURN_SUPPLIES` ? cu?i k? ho?ch d? tr? dúng v? cùng kho dã ch?n.
            - Không t?o `COLLECT_SUPPLIES` ? cu?i k? ho?ch n?u phía sau không có activity nào dùng s? hàng dó.

            ## QUY T?C T?NG LO?I ACTIVITY
            - `COLLECT_SUPPLIES`: ch? t?o cho v?t ph?m th?t s? l?y t? kho dã ch?n; `supplies_to_collect` ch? ch?a các item có trong kho dó.
            - `DELIVER_SUPPLIES`: giao dúng các v?t ph?m v?a l?y t? kho dã ch?n cho SOS tuong ?ng.
            - `RESCUE`: luôn t?o n?u hi?n tru?ng c?n c?u ngu?i, k? c? khi thi?t b? c?u h? b? thi?u; thi?u gì thì ghi vào `supply_shortages` và `special_notes`.
            - `MEDICAL_AID`: n?u thi?u v?t ph?m y t? thì v?n có th? t?o activity, nhung ph?i ghi rõ thi?u h?t.
            - `EVACUATE`: không l?y v?t ph?m ? bu?c này; ph?i ch?n `assembly_point_id` g?n n?n nhân nh?t.

            ## QUY T?C TEAM VÀ ASSEMBLY POINT
            - G?i `getTeams` d? l?y `team_id`; không t? b?a team ngoài k?t qu? công c?.
            - N?u l?c theo `ability` mà không th?y team, g?i l?i `getTeams` không truy?n ability tru?c khi ch?p nh?n `suggested_team = null`.
            - V?i `RESCUE` ho?c `EVACUATE`, b?t bu?c g?i `getAssemblyPoints` và ch?n `assembly_point_id` g?n n?n nhân nh?t.

            ## Ð?NH D?NG overall_assessment
            - Toàn b? n?i dung ph?i n?m trên m?t dòng duy nh?t.
            - Khi nh?c t?i SOS, dùng format `[SOS ID X]: ...`.

            ## JSON B?T BU?C
            - Tr? v? JSON thu?n, không markdown.
            - Ngoài các field mission hi?n có, luôn tr? thêm:
              - `needs_additional_depot`: boolean
              - `supply_shortages`: array
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
            SuggestedMissionTitle = ExtractStr(text, "mission_title") ?? "Nhi?m v? gi?i c?u",
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
            $"Plan hi?n dang dùng nhi?u kho: {depotLabel}. Backend yêu c?u AI ch? ch?n m?t kho phù h?p nh?t cho toàn mission.");
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
                    $"Activity step {activity.Step} ({activity.ActivityType}) chua có estimated_time h?p l?.");
                continue;
            }

            if (!TryParseDurationToMinutes(activity.EstimatedTime, out var activityMinutes))
            {
                result.NeedsManualReview = true;
                result.SpecialNotes = AppendSpecialNote(
                    result.SpecialNotes,
                    $"Activity step {activity.Step} ({activity.ActivityType}) có estimated_time khó hi?u: '{activity.EstimatedTime}'.");
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
                "Mission chua có estimated_duration h?p l? d? coordinator ki?m tra.");
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

        var hourMatch = Regex.Match(text, @"(?<value>\d+)\s*(gi?|gio|hour|hours|hr|hrs|h)");
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
            ? $"{hours} gi?"
            : $"{hours} gi? {minutes} phút";
    }

    private static string BuildShortageCoordinatorNote(IReadOnlyCollection<SupplyShortageDto> shortages)
    {
        var details = shortages
            .Select(shortage =>
            {
                var sosPrefix = shortage.SosRequestId.HasValue ? $"[SOS ID {shortage.SosRequestId.Value}] " : string.Empty;
                var unitSuffix = string.IsNullOrWhiteSpace(shortage.Unit) ? string.Empty : $" {shortage.Unit}";
                return $"{sosPrefix}{shortage.ItemName} thi?u x{shortage.MissingQuantity}{unitSuffix}";
            })
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return details.Count == 0
            ? "Coordinator c?n b? sung thêm kho/ngu?n c?p phát vì kho dã ch?n không d? v?t ph?m."
            : "Coordinator c?n b? sung thêm kho/ngu?n c?p phát. Thi?u: " + string.Join("; ", details) + ".";
    }

    private static bool IsCollectActivity(SuggestedActivityDto activity) =>
        string.Equals(activity.ActivityType, CollectSuppliesActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsReturnActivity(SuggestedActivityDto activity) =>
        string.Equals(activity.ActivityType, ReturnSuppliesActivityType, StringComparison.OrdinalIgnoreCase);

    private static bool IsReturnAssemblyPointActivity(SuggestedActivityDto activity) =>
        string.Equals(activity.ActivityType, ReturnAssemblyPointActivityType, StringComparison.OrdinalIgnoreCase);

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
                AssemblyPointId = activity.SuggestedTeam.AssemblyPointId,
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
                    splitActivity.Description = $"Ðua n?n nhân t? {sos.Latitude.Value}, {sos.Longitude.Value} (SOS ID {sosId}) d?n {splitActivity.AssemblyPointName ?? "di?m t?p k?t an toàn"}.";
                }
                else
                {
                    splitActivity.Description = $"Ðua n?n nhân c?a SOS ID {sosId} d?n {splitActivity.AssemblyPointName ?? "di?m t?p k?t an toàn"}.";
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
                        warnings.Add($"Mission suggestion dang có nhi?u RETURN_ASSEMBLY_POINT cho team #{group.Key}; backend ch? gi? m?t bu?c cu?i.");

                    return ordered.First();
                });

        var returnActivities = new List<SuggestedActivityDto>();
        foreach (var team in teamsById.Values.OrderBy(team => team.TeamName).ThenBy(team => team.TeamId))
        {
            if (!team.AssemblyPointId.HasValue
                || !team.Latitude.HasValue
                || !team.Longitude.HasValue)
            {
                warnings.Add($"Team #{team.TeamId} thi?u assembly_point_id ho?c t?a d? di?m t?p k?t; chua th? t? t?o RETURN_ASSEMBLY_POINT.");
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
            ? $"di?m t?p k?t #{team.AssemblyPointId}"
            : team.AssemblyPointName!;

        activity.Step = 0;
        activity.ActivityType = ReturnAssemblyPointActivityType;
        activity.Description = $"Hoàn t?t nhi?m v?, d?i {team.TeamName} quay v? di?m t?p k?t {assemblyPointName}.";
        activity.Priority = "Low";
        activity.EstimatedTime = DefaultReturnAssemblyEstimatedTime;
        activity.ExecutionMode = SingleTeamExecutionMode;
        activity.RequiredTeamCount = 1;
        activity.CoordinationGroupKey = null;
        activity.CoordinationNotes = "Ð?i quay v? di?m t?p k?t ban d?u sau khi hoàn t?t nhi?m v?.";
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
        activity.CoordinationNotes = "M?t d?i tr? v?t ph?m tái s? d?ng dã l?y tru?c dó v? l?i kho ngu?n.";
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
            ? $"Hoàn t?t nhi?m v?, dua v?t ph?m tái s? d?ng v? l?i {depotLabel}."
            : $"Hoàn t?t nhi?m v?, dua v?t ph?m tái s? d?ng v? l?i {depotLabel}. Tr?: {itemSummary}.";
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

            // If name is already in the description ? just remove the duplicate coordinates.
            // Otherwise ? replace the coordinate pair with the name.
            return nameAlreadyPresent ? string.Empty : name;
        });

        // Clean up double spaces or leading/trailing whitespace left after removal
        return Regex.Replace(result, @"  +", " ").Trim();
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
        var availableNearbyTeams = nearbyTeams ?? [];
        MissionSuggestionMetadata? pipelineMetadata = null;
        int? suggestionId = null;

        if (_pipelineOptions.UseMissionSuggestionPipeline)
        {
            pipelineMetadata = CreateSuggestionMetadataForPipeline();
            suggestionId = await EnsureSuggestionRecordAsync(clusterId, pipelineMetadata, cancellationToken);

            await using var pipelineEnumerator = GeneratePipelineSuggestionStreamAsync(
                sosRequests,
                nearbyDepots,
                availableNearbyTeams,
                isMultiDepotRecommended,
                clusterId,
                suggestionId,
                pipelineMetadata,
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

        yield return Status("Ðang t?i c?u hình AI agent...");

        var prompt = await _promptRepository.GetActiveByTypeAsync(PromptType.MissionPlanning, cancellationToken);
        if (prompt == null)
        {
            yield return Error("Chua có prompt 'MissionPlanning' dang du?c kích ho?t. Vui lòng c?u hình trong qu?n tr? h? th?ng.");
            yield break;
        }

        var settings = _settingsResolver.Resolve(
            prompt,
            new AiPromptExecutionFallback(
                FallbackModel,
                FallbackApiUrl,
                FallbackTemperature,
                FallbackMaxTokens));

        // Enforce minimum 32K tokens - mission plans with tool calls can be very long
        var maxTokens = Math.Max(settings.MaxTokens, 32768);

        // Build the initial user message (no pre-loaded depot data; agent fetches via tools)
        var sosDataJson = BuildSosRequestsData(sosRequests);
        var userMessage = (prompt.UserPromptTemplate ?? string.Empty)
            .Replace("{{sos_requests_data}}", sosDataJson)
            .Replace("{{total_count}}", sosRequests.Count.ToString())
            .Replace("{{depots_data}}", "(D? li?u kho không du?c truy?n tr?c ti?p. Hãy g?i công c? searchInventory d? tra c?u v?t ph?m kh? d?ng trong các kho h?p l? c?a cluster hi?n t?i, sau dó ch?n dúng m?t kho phù h?p nh?t cho toàn mission.)")
            .TrimEnd();

        var nearbyTeamsNote = availableNearbyTeams.Count > 0
            ? $"\n\nD? li?u d?i c?u h? không du?c truy?n tr?c ti?p. Hãy g?i công c? getTeams d? xem {availableNearbyTeams.Count} d?i nearby currently available trong bán kính cluster. Công c? này ch? tr? v? các d?i g?n nh?t trong pool dó, không bao gi? m? r?ng ra team xa hon."
            : "\n\nHi?n không có d?i Available nào trong bán kính cluster. N?u công c? getTeams tr? v? r?ng, không du?c t? b?a team ngoài vùng; hãy d? suggested_team = null và ghi rõ c?n manual review.";

        userMessage += nearbyTeamsNote;

        var systemPrompt = (prompt.SystemPrompt ?? string.Empty).TrimEnd()
            + "\n\n" + BuildAgentInstructions(isMultiDepotRecommended);

        yield return Status($"AI agent ({settings.Provider}/{settings.Model}) dang phân tích {sosRequests.Count} SOS request...");

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
                yield return Error($"L?i k?t n?i t?i AI: {sendError}");
                yield break;
            }

            if (response == null)
            {
                yield return Error("AI không ph?n h?i. Vui lòng th? l?i sau.");
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
                yield return Error($"AI tr? v? l?i ({response.HttpStatusCode}). Vui lòng th? l?i sau.");
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
                yield return Error($"Yêu c?u b? ch?n b?i b? l?c AI ({response.BlockReason}). Vui lòng th? l?i ho?c di?u ch?nh n?i dung SOS.");
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
                    yield return Error($"N?i dung b? l?c b?i AI ({finishReason}). Vui lòng th? l?i sau.");
                    yield break;
                }
                if (turn == 0 && finishReason is "MAX_TOKENS")
                {
                    yield return Error("AI vu?t gi?i h?n token ? lu?t d?u. Vui lòng th? l?i.");
                    yield break;
                }

                yield return Error($"AI không tr? v? n?i dung (finishReason={finishReason}). Vui lòng th? l?i.");
                yield break;
            }

            messages.Add(AiChatMessage.Assistant(response.Text, response.ToolCalls));

            if (response.ToolCalls.Count == 0)
            {
                // No function calls ? final answer
                finalText = response.Text;
                break;
            }

            // Execute each function call
            foreach (var toolCall in response.ToolCalls)
            {
                yield return Status($"Agent dang g?i công c?: {toolCall.Name}(...)");

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

                yield return Status($"Công c? {toolCall.Name}() dã tr? v? k?t qu?.");
                messages.Add(AiChatMessage.Tool(toolCall.Id, toolCall.Name, toolResult));
            }
        }

        if (string.IsNullOrWhiteSpace(finalText))
        {
            yield return Error("AI agent không dua ra ph?n h?i cu?i cùng sau t?i da s? vòng l?p cho phép.");
            yield break;
        }

        yield return Status("Ðang x? lý k?t qu?...");

        _logger.LogDebug("Raw AI response (final turn):\n{raw}", finalText);

        var result       = ParseMissionSuggestion(finalText);
        result.IsSuccess     = true;
        result.ModelName     = settings.Model;
        result.RawAiResponse = finalText;
        await FinalizeSuggestionResultAsync(
            result,
            sosRequests,
            nearbyDepots,
            availableNearbyTeams,
            isMultiDepotRecommended,
            clusterId,
            suggestionId,
            pipelineMetadata,
            null,
            "legacy",
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
            Description = "Tìm ki?m v?t ph?m dang kh? d?ng theo danh m?c và lo?i trong các kho h?p l? c?a cluster hi?n t?i. Tr? v? c? consumable l?n reusable v?i item_id, tên, item_type, available_quantity, kho ch?a và t?a d? v? trí kho (depot_latitude, depot_longitude). Reusable còn có good_available_count, fair_available_count, poor_available_count.",
            Parameters = ParseJson(
                """
                {
                  "type": "object",
                  "properties": {
                    "category": { "type": "string", "description": "Tên danh m?c v?t ph?m, ví d?: 'Nu?c', 'Th?c ph?m', 'Y t?', 'Qu?n áo'" },
                    "type": { "type": "string", "description": "Tên lo?i ho?c tên v?t ph?m c? th? trong danh m?c (tu? ch?n)" },
                    "page": { "type": "integer", "description": "S? trang (b?t d?u t? 1)" }
                  },
                  "required": ["category"]
                }
                """)
        },
        new()
        {
            Name = "getTeams",
            Description = "Tìm ki?m d?i c?u h? trong pool nearby teams c?a cluster hi?n t?i. Có th? l?c theo lo?i k? nang/team_type. Tr? v? team_id, tên, lo?i, tr?ng thái, s? thành viên, v? trí di?m t?p k?t (assembly_point_name, latitude, longitude) và distance_km.",
            Parameters = ParseJson(
                """
                {
                  "type": "object",
                  "properties": {
                    "ability": { "type": "string", "description": "L?c theo lo?i k? nang/team_type (tu? ch?n)" },
                    "available": { "type": "boolean", "description": "Ch? mang tính tuong thích. Công c? này luôn ch? tr? v? nearby teams dang Available; truy?n false cung không m? r?ng ph?m vi." },
                    "page": { "type": "integer", "description": "S? trang (b?t d?u t? 1)" }
                  },
                  "required": []
                }
                """)
        },
        new()
        {
            Name = "getAssemblyPoints",
            Description = "L?y danh sách di?m t?p k?t dang ho?t d?ng d? ch?n noi t?p k?t g?n nh?t cho activity RESCUE ho?c EVACUATE. Tr? v? assembly_point_id, tên, s?c ch?a t?i da và t?a d?.",
            Parameters = ParseJson(
                """
                {
                  "type": "object",
                  "properties": {
                    "page": { "type": "integer", "description": "S? trang (b?t d?u t? 1)" }
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

