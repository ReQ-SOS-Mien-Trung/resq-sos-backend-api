using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;

public class GenerateRescueMissionSuggestionCommandHandler(
    ISosClusterRepository sosClusterRepository,
    ISosRequestRepository sosRequestRepository,
    IRescueMissionSuggestionService suggestionService,
    IMissionAiSuggestionRepository missionAiSuggestionRepository,
    IDepotRepository depotRepository,
    ILogger<GenerateRescueMissionSuggestionCommandHandler> logger
) : IRequestHandler<GenerateRescueMissionSuggestionCommand, GenerateRescueMissionSuggestionResponse>
{
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly IRescueMissionSuggestionService _suggestionService = suggestionService;
    private readonly IMissionAiSuggestionRepository _missionAiSuggestionRepository = missionAiSuggestionRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly ILogger<GenerateRescueMissionSuggestionCommandHandler> _logger = logger;

    // Số kho gần nhất tối đa gửi cho AI
    private const int MaxDepotContext = 5;

    // Bán kính (km) lọc kho gần - chỉ xét kho trong phạm vi này trước khi so vật tư
    private const double MaxDepotRadiusKm = 20.0;

    // Nếu confidence_score của AI dưới ngưỡng này → đánh dấu cần xét lại thủ công
    private const double LowConfidenceThreshold = 0.65;

    // Thứ tự ưu tiên: giá trị càng cao càng quan trọng
    private static readonly Dictionary<string, int> PriorityRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Low"]      = 1,
        ["Medium"]   = 2,
        ["High"]     = 3,
        ["Critical"] = 4
    };

    public async Task<GenerateRescueMissionSuggestionResponse> Handle(
        GenerateRescueMissionSuggestionCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Generating rescue mission suggestion for ClusterId={clusterId}, RequestedBy={userId}",
            request.ClusterId, request.RequestedByUserId);

        // 1. Validate cluster exists
        var cluster = await _sosClusterRepository.GetByIdAsync(request.ClusterId, cancellationToken);
        if (cluster is null)
            throw new NotFoundException($"Không tìm thấy cluster với ID: {request.ClusterId}");

        // 2. Load all SOS requests belonging to the cluster
        var clusterSosRequests = await _sosRequestRepository.GetByClusterIdAsync(request.ClusterId, cancellationToken);
        var sosRequestList = clusterSosRequests.ToList();

        if (sosRequestList.Count == 0)
            throw new BadRequestException($"Cluster {request.ClusterId} không có SOS request nào");

        var sosRequestSummaries = sosRequestList.Select(sos => new SosRequestSummary
        {
            Id = sos.Id,
            SosType = sos.SosType,
            RawMessage = sos.RawMessage,
            StructuredData = sos.StructuredData,
            PriorityLevel = sos.PriorityLevel?.ToString(),
            Status = sos.Status.ToString(),
            Latitude = sos.Location?.Latitude,
            Longitude = sos.Location?.Longitude,
            WaitTimeMinutes = sos.WaitTimeMinutes,
            CreatedAt = sos.CreatedAt
        }).ToList();

        // 3. Trích xuất danh sách vật tư cần thiết từ StructuredData.supplies của các SOS request
        var neededSupplies = ExtractNeededSupplies(sosRequestSummaries);

        // 4. Tìm SOS request quan trọng nhất để tính khoảng cách đến kho
        var anchorSos = FindHighestPrioritySos(sosRequestSummaries);

        // 5. Query DB: lấy các kho đang hoạt động và còn hàng,
        //    ưu tiên kho đáp ứng được nhiều vật tư cần thiết nhất (theo SOS request), sau đó mới tính gần nhất
        var (nearbyDepots, multiDepotRecommended) = await BuildNearbyDepotSummariesAsync(anchorSos, neededSupplies, cancellationToken);

        _logger.LogInformation(
            "Depot context: AnchorSOS={sosId} (Priority={priority}, Lat={lat}, Lng={lng}), NeededSupplies=[{supplies}], EligibleDepots={count}, MultiDepot={multi}",
            anchorSos?.Id, anchorSos?.PriorityLevel, anchorSos?.Latitude, anchorSos?.Longitude,
            string.Join(", ", neededSupplies), nearbyDepots.Count, multiDepotRecommended);

        // 5. Call AI to generate suggestion
        var result = await _suggestionService.GenerateSuggestionAsync(
            sosRequestSummaries, nearbyDepots, multiDepotRecommended, cancellationToken);

        // 6. Check AI confidence — flag low-confidence results for manual review
        if (result.IsSuccess && result.ConfidenceScore < LowConfidenceThreshold)
        {
            result.NeedsManualReview = true;
            result.LowConfidenceWarning =
                $"AI chỉ đạt độ tự tin {result.ConfidenceScore:P0} (ngưỡng: {LowConfidenceThreshold:P0}). " +
                "Kế hoạch có thể chưa chính xác — điều phối viên nên xem xét và điều chỉnh thủ công.";
            _logger.LogWarning(
                "AI low-confidence result for ClusterId={clusterId}: ConfidenceScore={score}",
                request.ClusterId, result.ConfidenceScore);
        }
        result.MultiDepotRecommended = multiDepotRecommended;

        _logger.LogInformation(
            "Rescue mission suggestion result: IsSuccess={isSuccess}, Title={title}, ResponseTime={time}ms, Confidence={conf}, NeedsReview={review}, MultiDepot={multi}",
            result.IsSuccess, result.SuggestedMissionTitle, result.ResponseTimeMs,
            result.ConfidenceScore, result.NeedsManualReview, result.MultiDepotRecommended);

        // 6. Persist to DB (always save, even partial results)
        int? savedSuggestionId = null;
        try
        {
            var activitiesJson = result.SuggestedActivities.Count > 0
                ? JsonSerializer.Serialize(result.SuggestedActivities)
                : null;

            var metadataJson = JsonSerializer.Serialize(new
            {
                result.OverallAssessment,
                result.EstimatedDuration,
                result.SpecialNotes,
                result.SuggestedResources,
                result.SuggestedSeverityLevel,
                result.SuggestedMissionType,
                result.RawAiResponse
            });

            var missionModel = new MissionAiSuggestionModel
            {
                ClusterId = request.ClusterId,
                ModelName = result.ModelName,
                AnalysisType = "RescueMissionSuggestion",
                SuggestedMissionTitle = result.SuggestedMissionTitle,
                SuggestedPriorityScore = result.SuggestedPriorityScore,
                ConfidenceScore = result.ConfidenceScore,
                Metadata = metadataJson,
                CreatedAt = DateTime.UtcNow,
                Activities = activitiesJson is not null
                    ? [
                        new ActivityAiSuggestionModel
                        {
                            ClusterId = request.ClusterId,
                            ModelName = result.ModelName,
                            ActivityType = result.SuggestedMissionType ?? "RescueActivities",
                            SuggestionPhase = "Execution",
                            SuggestedActivities = activitiesJson,
                            ConfidenceScore = result.ConfidenceScore,
                            CreatedAt = DateTime.UtcNow
                        }
                      ]
                    : []
            };

            savedSuggestionId = await _missionAiSuggestionRepository.CreateAsync(missionModel, cancellationToken);
            _logger.LogInformation("Saved mission suggestion to DB: SuggestionId={id}", savedSuggestionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist mission suggestion to DB for ClusterId={clusterId}", request.ClusterId);
        }

        // 7. Map result to response
        return new GenerateRescueMissionSuggestionResponse
        {
            SuggestionId = savedSuggestionId,
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            ModelName = result.ModelName,
            ResponseTimeMs = result.ResponseTimeMs,
            SosRequestCount = sosRequestSummaries.Count,
            SuggestedMissionTitle = result.SuggestedMissionTitle,
            SuggestedMissionType = result.SuggestedMissionType,
            SuggestedPriorityScore = result.SuggestedPriorityScore,
            SuggestedSeverityLevel = result.SuggestedSeverityLevel,
            OverallAssessment = result.OverallAssessment,
            SuggestedActivities = result.SuggestedActivities,
            SuggestedResources = result.SuggestedResources,
            EstimatedDuration = result.EstimatedDuration,
            SpecialNotes = result.SpecialNotes,
            ConfidenceScore = result.ConfidenceScore,
            NeedsManualReview = result.NeedsManualReview,
            LowConfidenceWarning = result.LowConfidenceWarning,
            MultiDepotRecommended = result.MultiDepotRecommended
        };
    }

    /// <summary>
    /// Tìm SOS request có mức ưu tiên cao nhất trong cluster để làm điểm neo tính khoảng cách.
    /// Tiebreaker: chờ lâu nhất (WaitTimeMinutes desc) → tạo sớm nhất (CreatedAt asc).
    /// </summary>
    private static SosRequestSummary? FindHighestPrioritySos(List<SosRequestSummary> sosRequests)
    {
        return sosRequests
            .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
            .OrderByDescending(s => PriorityRank.TryGetValue(s.PriorityLevel ?? string.Empty, out var rank) ? rank : 0)
            .ThenByDescending(s => s.WaitTimeMinutes ?? 0)
            .ThenBy(s => s.CreatedAt ?? DateTime.MaxValue)
            .FirstOrDefault();
    }

    /// <summary>
    /// Query DB lấy danh sách kho đang hoạt động có hàng, sắp xếp theo:<br/>
    /// 1. Ưu tiên kho đáp ứng được nhiều vật tư cần thiết (theo SOS request) nhất trước;<br/>
    /// 2. Cùng độ phủ thì lấy kho gần SOS nhất.<br/>
    /// Nếu không có kho nào bao phủ đủ tất cả vật tư cần thiết → <c>multiDepotRecommended = true</c>.
    /// </summary>
    private async Task<(List<DepotSummary> Depots, bool MultiDepotRecommended)> BuildNearbyDepotSummariesAsync(
        SosRequestSummary? anchorSos,
        IReadOnlyCollection<string> neededSupplies,
        CancellationToken cancellationToken)
    {
        if (anchorSos is null)
        {
            _logger.LogWarning("Không có SOS request nào có toạ độ GPS — bỏ qua context kho tiếp tế.");
            return ([], false);
        }

        // Query DB: kho Status=Available, CurrentUtilization > 0, kèm tồn kho chi tiết
        var availableDepots = (await _depotRepository.GetAvailableDepotsAsync(cancellationToken)).ToList();

        if (availableDepots.Count == 0)
        {
            _logger.LogWarning("Không có kho nào đủ điều kiện (Available + còn hàng).");
            return ([], false);
        }

        // Tính khoảng cách và lọc kho trong bán kính MaxDepotRadiusKm;
        // nếu không kho nào đủ gần thì mở rộng lấy tất cả (fallback)
        var withDistance = availableDepots
            .Where(d => d.Location is not null)
            .Select(d =>
            {
                double distKm = HaversineKm(
                    anchorSos.Latitude!.Value, anchorSos.Longitude!.Value,
                    d.Location!.Latitude, d.Location!.Longitude);
                return (depot: d, distKm: Math.Round(distKm, 2));
            })
            .ToList();

        var nearby = withDistance.Where(x => x.distKm <= MaxDepotRadiusKm).ToList();
        if (nearby.Count == 0)
        {
            _logger.LogWarning(
                "Không có kho nào trong bán kính {radius} km — mở rộng tìm toàn bộ kho.",
                MaxDepotRadiusKm);
            nearby = withDistance; // fallback: lấy tất cả
        }

        // Trong các kho gần, ưu tiên kho đáp ứng nhiều vật tư cần thiết nhất;
        // cùng mức phủ thì ưu tiên kho gần hơn
        var scored = nearby
            .Select(x =>
            {
                int coverageScore = neededSupplies.Count > 0
                    ? CountCoveredSupplies(x.depot.InventoryLines, neededSupplies)
                    : x.depot.InventoryLines.Count;
                return (x.depot, x.distKm, coverageScore);
            })
            .OrderByDescending(x => x.coverageScore)
            .ThenBy(x => x.distKm)
            .ToList();

        // Multi-depot: kho tốt nhất chưa bao phủ đủ tất cả vật tư cần thiết
        int totalNeeded = neededSupplies.Count;
        int maxCoverage = scored.Count > 0 ? scored.Max(x => x.coverageScore) : 0;
        bool multiDepotRecommended =
            totalNeeded > 0
            && scored.Count >= 2
            && maxCoverage < totalNeeded;

        var depotSummaries = scored
            .Take(MaxDepotContext)
            .Select(x => new DepotSummary
            {
                Id = x.depot.Id,
                Name = x.depot.Name,
                Address = x.depot.Address,
                Latitude = x.depot.Location!.Latitude,
                Longitude = x.depot.Location.Longitude,
                DistanceKm = x.distKm,
                Capacity = x.depot.Capacity,
                CurrentUtilization = x.depot.CurrentUtilization,
                Status = x.depot.Status.ToString(),
                Inventories = x.depot.InventoryLines
                    .Select(l => new DepotInventoryItemDto
                    {
                        ItemName = l.ItemName,
                        Unit = l.Unit,
                        AvailableQuantity = l.AvailableQuantity
                    })
                    .ToList()
            })
            .ToList();

        return (depotSummaries, multiDepotRecommended);
    }

    /// <summary>
    /// Trích xuất danh sách loại vật tư cần thiết từ StructuredData.supplies của tất cả SOS request trong cluster.
    /// </summary>
    private static HashSet<string> ExtractNeededSupplies(List<SosRequestSummary> sosRequests)
    {
        var needed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sos in sosRequests)
        {
            if (string.IsNullOrWhiteSpace(sos.StructuredData)) continue;
            try
            {
                using var doc = JsonDocument.Parse(sos.StructuredData);
                if (doc.RootElement.TryGetProperty("supplies", out var supplies)
                    && supplies.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in supplies.EnumerateArray())
                    {
                        var val = item.GetString();
                        if (!string.IsNullOrWhiteSpace(val))
                            needed.Add(val.Trim().ToUpperInvariant());
                    }
                }
            }
            catch (JsonException) { /* bỏ qua StructuredData không hợp lệ */ }
        }
        return needed;
    }

    /// <summary>
    /// Mapping từ supply category code trong SOS request sang keywords xuất hiện trong tên vật tư kho.
    /// </summary>
    private static readonly Dictionary<string, string[]> SupplyCategoryKeywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["FOOD"]             = ["food", "gạo", "mì", "lương thực", "thực phẩm", "bánh", "đồ ăn"],
            ["WATER"]            = ["water", "nước"],
            ["MEDICINE"]         = ["medicine", "thuốc", "y tế", "medical", "băng", "bông", "sơ cứu"],
            ["RESCUE_EQUIPMENT"] = ["rescue", "cứu hộ", "dây", "phao", "xuồng", "thuyền"],
            ["HYGIENE"]          = ["hygiene", "vệ sinh", "xà phòng", "khăn", "giấy vệ sinh"],
            ["SHELTER"]          = ["shelter", "lều", "bạt", "tấm che"],
            ["CLOTHING"]         = ["clothing", "quần áo", "áo", "chăn", "mền"],
            ["TRANSPORTATION"]   = ["transportation", "xe", "phương tiện"],
        };

    /// <summary>
    /// Đếm số loại vật tư trong <paramref name="neededSupplies"/> mà kho này có sẵn trong tồn kho.
    /// Dùng keyword mapping từ category code của SOS sang tên vật tư thực tế.
    /// </summary>
    private static int CountCoveredSupplies(
        IEnumerable<DepotInventoryLine> inventoryLines,
        IReadOnlyCollection<string> neededSupplies)
    {
        var itemNamesUpper = inventoryLines
            .Select(l => l.ItemName.ToUpperInvariant())
            .ToList();

        return neededSupplies.Count(supply =>
        {
            if (SupplyCategoryKeywords.TryGetValue(supply, out var keywords))
                return itemNamesUpper.Any(n =>
                    keywords.Any(kw => n.Contains(kw, StringComparison.OrdinalIgnoreCase)));
            // Fallback: khớp trực tiếp tên supply với tên vật tư
            return itemNamesUpper.Any(n => n.Contains(supply, StringComparison.OrdinalIgnoreCase));
        });
    }

    /// <summary>
    /// Tính khoảng cách Haversine (km) giữa hai toạ độ GPS.
    /// </summary>
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0; // bán kính Trái Đất (km)
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRad(double degrees) => degrees * Math.PI / 180.0;
}
