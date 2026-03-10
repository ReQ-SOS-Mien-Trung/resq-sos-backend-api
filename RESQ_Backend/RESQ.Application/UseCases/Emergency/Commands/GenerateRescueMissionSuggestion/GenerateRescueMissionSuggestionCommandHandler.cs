using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
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
    IUnitOfWork unitOfWork,
    ILogger<GenerateRescueMissionSuggestionCommandHandler> logger
) : IRequestHandler<GenerateRescueMissionSuggestionCommand, GenerateRescueMissionSuggestionResponse>
{
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly IRescueMissionSuggestionService _suggestionService = suggestionService;
    private readonly IMissionAiSuggestionRepository _missionAiSuggestionRepository = missionAiSuggestionRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<GenerateRescueMissionSuggestionCommandHandler> _logger = logger;

    // Số kho gần nhất tối đa gửi cho AI
    private const int MaxDepotContext = 5;

    // Bán kính (km) lọc kho gần - chỉ xét kho trong phạm vi này, KHÔNG mở rộng ra ngoài
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

        // 5b. Post-process: backfill fields AI often leaves null (item_id, sos_request_id)
        if (result.IsSuccess && result.SuggestedActivities.Count > 0)
        {
            BackfillItemIds(result.SuggestedActivities, nearbyDepots);
            BackfillSosRequestIds(result.SuggestedActivities, sosRequestSummaries);
        }

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

            // Mark cluster as having a mission suggestion generated
            cluster.IsMissionCreated = true;
            await _sosClusterRepository.UpdateAsync(cluster, cancellationToken);
            await _unitOfWork.SaveAsync();
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
    /// Post-processing: điền item_id cho các supply mà AI bỏ trống, bằng cách fuzzy-match tên vật tư
    /// với danh sách tồn kho thực tế từ các kho đã gửi cho AI.
    /// </summary>
    private static void BackfillItemIds(List<SuggestedActivityDto> activities, List<DepotSummary> depots)
    {
        // Build lookup: normalized item name → ItemId
        var itemLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var depot in depots)
            foreach (var inv in depot.Inventories)
                if (inv.ItemId.HasValue && !string.IsNullOrEmpty(inv.ItemName))
                    itemLookup.TryAdd(NormalizeItemName(inv.ItemName), inv.ItemId.Value);

        if (itemLookup.Count == 0) return;

        foreach (var activity in activities)
        {
            if (activity.SuppliesToCollect is null) continue;
            foreach (var supply in activity.SuppliesToCollect)
            {
                if (supply.ItemId.HasValue || string.IsNullOrEmpty(supply.ItemName)) continue;
                var normalized = NormalizeItemName(supply.ItemName);

                // 1. Exact match
                if (itemLookup.TryGetValue(normalized, out var exactId))
                {
                    supply.ItemId = exactId;
                    continue;
                }

                // 2. Partial match: depot item name is contained in supply name, or vice versa
                foreach (var (key, id) in itemLookup)
                {
                    if (normalized.Contains(key) || key.Contains(normalized))
                    {
                        supply.ItemId = id;
                        break;
                    }
                }
            }
        }
    }

    private static string NormalizeItemName(string name) =>
        name.ToLowerInvariant()
            .Replace("&", " ").Replace("(", " ").Replace(")", " ")
            .Replace(",", " ").Replace("-", " ").Replace("/", " ")
            .Replace("  ", " ").Trim();

    // Regex để trích tọa độ dạng "16.6395, 107.2945" hoặc "16.6395,107.2945" từ description
    private static readonly System.Text.RegularExpressions.Regex CoordRegex =
        new(@"(-?\d{1,3}\.\d+)\s*,\s*(-?\d{1,3}\.\d+)",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Post-processing: điền sos_request_id cho các activity mà AI bỏ trống.<br/>
    /// • Cluster 1 SOS → gán thẳng.<br/>
    /// • Cluster nhiều SOS → trích tọa độ từ description, khớp SOS gần nhất (chỉ khi SOS có GPS).
    ///   Nếu không trích được tọa độ → gán SOS có độ ưu tiên cao nhất làm fallback.
    /// </summary>
    private static void BackfillSosRequestIds(List<SuggestedActivityDto> activities, List<SosRequestSummary> sosRequests)
    {
        if (sosRequests.Count == 0) return;

        // Case 1: single SOS — trivial
        if (sosRequests.Count == 1)
        {
            var id = sosRequests[0].Id;
            foreach (var a in activities)
                a.SosRequestId ??= id;
            return;
        }

        // Case 2: multiple SOS — try coordinate-based matching
        var sosWithGps = sosRequests
            .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
            .ToList();

        // Fallback SOS (highest priority, used when coords can't be determined)
        var fallbackSos = sosRequests
            .OrderByDescending(s => PriorityRank.TryGetValue(s.PriorityLevel ?? string.Empty, out var r) ? r : 0)
            .ThenByDescending(s => s.WaitTimeMinutes ?? 0)
            .First();

        foreach (var activity in activities)
        {
            if (activity.SosRequestId.HasValue) continue;

            if (sosWithGps.Count > 0 && !string.IsNullOrEmpty(activity.Description))
            {
                var match = CoordRegex.Match(activity.Description);
                if (match.Success
                    && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var lat)
                    && double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var lon))
                {
                    var nearest = sosWithGps
                        .OrderBy(s => HaversineKm(lat, lon, s.Latitude!.Value, s.Longitude!.Value))
                        .First();
                    activity.SosRequestId = nearest.Id;
                    continue;
                }
            }

            // No coords found — use highest-priority SOS as fallback
            activity.SosRequestId = fallbackSos.Id;
        }
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
            _logger.LogWarning("Không có SOS request nào có toạ độ GPS — không thể lọc kho tiếp tế.");
            throw new BadRequestException("Không lọc được kho phù hợp vì không có SOS request nào có toạ độ GPS. Vui lòng tự lập kế hoạch thủ công.");
        }

        // Query DB: kho Status=Available, CurrentUtilization > 0, kèm tồn kho chi tiết
        var availableDepots = (await _depotRepository.GetAvailableDepotsAsync(cancellationToken)).ToList();

        if (availableDepots.Count == 0)
        {
            _logger.LogWarning("Không có kho nào đủ điều kiện (Available + còn hàng).");
            throw new BadRequestException("Không lọc được kho phù hợp: hiện không có kho nào đang hoạt động và còn hàng. Vui lòng tự lập kế hoạch thủ công.");
        }

        // Tính khoảng cách và lọc kho trong bán kính MaxDepotRadiusKm;
        // kho nằm ngoài bán kính này sẽ bị loại hoàn toàn — không có fallback mở rộng
        var nearby = availableDepots
            .Where(d => d.Location is not null)
            .Select(d =>
            {
                double distKm = HaversineKm(
                    anchorSos.Latitude!.Value, anchorSos.Longitude!.Value,
                    d.Location!.Latitude, d.Location!.Longitude);
                return (depot: d, distKm: Math.Round(distKm, 2));
            })
            .Where(x => x.distKm <= MaxDepotRadiusKm)
            .ToList();

        if (nearby.Count == 0)
        {
            _logger.LogWarning(
                "Không có kho nào trong bán kính {radius} km tính từ SOS request #{sosId}.",
                MaxDepotRadiusKm, anchorSos.Id);
            throw new BadRequestException($"Không lọc được kho phù hợp: không có kho nào trong bán kính {MaxDepotRadiusKm} km. Vui lòng tự lập kế hoạch thủ công.");
        }

        // Tính coverage bitmask cho từng kho (mỗi bit = 1 supply trong neededSupplies)
        var supplyList = neededSupplies.ToList();
        int totalNeeded = supplyList.Count;

        // Gán coverage bitmask cho mỗi kho trong bán kính;
        // pre-compute itemNamesUpper một lần mỗi kho để tránh lặp ToUpperInvariant trong vòng lặp supply
        var candidates = nearby
            .Select(x =>
            {
                var itemNamesUpper = x.depot.InventoryLines
                    .Select(l => l.ItemName.ToUpperInvariant())
                    .ToList();

                int mask = 0;
                if (totalNeeded > 0)
                {
                    for (int i = 0; i < supplyList.Count; i++)
                    {
                        if (CoversSingleSupply(itemNamesUpper, supplyList[i]))
                            mask |= (1 << i);
                    }
                }
                return (x.depot, x.distKm, mask);
            })
            .Where(x => totalNeeded == 0 || x.mask > 0) // loại kho không có vật tư nào liên quan
            .OrderByDescending(x => BitCount(x.mask))   // ưu tiên kho phủ nhiều supplies nhất
            .ThenBy(x => x.distKm)                       // tiebreaker: gần nhất
            .Take(MaxDepotContext)                        // giới hạn tối đa MaxDepotContext kho gửi cho AI
            .ToList();

        if (candidates.Count == 0)
        {
            _logger.LogWarning("Không có kho nào trong bán kính {radius} km có vật tư phù hợp.", MaxDepotRadiusKm);
            throw new BadRequestException($"Không lọc được kho phù hợp: các kho trong bán kính {MaxDepotRadiusKm} km không có vật tư nào khớp với nhu cầu SOS. Vui lòng tự lập kế hoạch thủ công.");
        }

        // Nếu không có danh sách supplies → trả về kho gần nhất
        if (totalNeeded == 0)
        {
            var nearest = candidates.First();
            return (
                [MapToDepotSummary(nearest.depot, nearest.distKm)],
                false
            );
        }

        int fullMask = (1 << totalNeeded) - 1;

        // Tìm tập con nhỏ nhất bao phủ tất cả supplies (exhaustive search trên bitmask)
        // Số lượng kho candidates thực tế nhỏ (≤ MaxDepotContext) nên chi phí chấp nhận được
        var bestCover = FindMinimumSetCover(candidates, fullMask);

        bool multiDepotRecommended;
        List<(DepotModel depot, double distKm, int mask)> chosen;

        if (bestCover is not null)
        {
            // Tìm được bộ kho tối ưu bao phủ đủ tất cả supplies
            chosen = bestCover;
            multiDepotRecommended = chosen.Count > 1;
            _logger.LogInformation(
                "Minimum set cover: {count} kho ({names}) bao phủ đủ {total} loại vật tư.",
                chosen.Count,
                string.Join(", ", chosen.Select(c => c.depot.Name)),
                totalNeeded);
        }
        else
        {
            // Không có tập kho nào bao phủ đủ → greedy maximize coverage với ít kho nhất
            chosen = GreedyMaxCover(candidates, fullMask);
            multiDepotRecommended = true;
            _logger.LogWarning(
                "Không thể bao phủ đủ {total} loại vật tư từ các kho trong bán kính {radius} km. " +
                "Greedy chọn {count} kho tối ưu nhất.",
                totalNeeded, MaxDepotRadiusKm, chosen.Count);
        }

        var depotSummaries = chosen
            .Select(x => MapToDepotSummary(x.depot, x.distKm))
            .ToList();

        return (depotSummaries, multiDepotRecommended);
    }

    /// <summary>
    /// Tìm tập con nhỏ nhất trong <paramref name="candidates"/> bao phủ đủ <paramref name="fullMask"/>.
    /// Duyệt exhaustive theo thứ tự kích thước tăng dần — hiệu quả vì số kho nhỏ (≤ 15).
    /// Khi có nhiều tập cùng kích thước, ưu tiên tập có tổng khoảng cách nhỏ nhất.
    /// </summary>
    private static List<(DepotModel depot, double distKm, int mask)>? FindMinimumSetCover(
        List<(DepotModel depot, double distKm, int mask)> candidates,
        int fullMask)
    {
        int n = candidates.Count;
        // Duyệt tất cả subset theo thứ tự kích thước tăng dần
        for (int size = 1; size <= n; size++)
        {
            var best = EnumerateSubsets(candidates, n, size, fullMask);
            if (best is not null) return best;
        }
        return null;
    }

    /// <summary>
    /// Liệt kê tất cả subset kích thước <paramref name="size"/> và trả về subset đầu tiên bao phủ đủ
    /// <paramref name="fullMask"/>. Trong cùng kích thước, ưu tiên subset có tổng distKm nhỏ nhất.
    /// </summary>
    private static List<(DepotModel depot, double distKm, int mask)>? EnumerateSubsets(
        List<(DepotModel depot, double distKm, int mask)> candidates,
        int n, int size, int fullMask)
    {
        List<(DepotModel depot, double distKm, int mask)>? bestSubset = null;
        double bestDist = double.MaxValue;

        void Recurse(int start, int combinedMask, List<(DepotModel, double, int)> current)
        {
            if (current.Count == size)
            {
                if ((combinedMask & fullMask) == fullMask)
                {
                    double totalDist = current.Sum(x => x.Item2);
                    if (totalDist < bestDist)
                    {
                        bestDist = totalDist;
                        bestSubset = [.. current];
                    }
                }
                return;
            }
            int remaining = size - current.Count;
            for (int i = start; i <= n - remaining; i++)
            {
                current.Add(candidates[i]);
                Recurse(i + 1, combinedMask | candidates[i].mask, current);
                current.RemoveAt(current.Count - 1);
            }
        }

        Recurse(0, 0, []);
        return bestSubset;
    }

    /// <summary>
    /// Greedy set cover: lần lượt chọn kho bao phủ nhiều supplies mới nhất,
    /// tiebreaker theo khoảng cách gần nhất. Dùng khi không thể cover đủ.
    /// </summary>
    private static List<(DepotModel depot, double distKm, int mask)> GreedyMaxCover(
        List<(DepotModel depot, double distKm, int mask)> candidates,
        int fullMask)
    {
        var chosen = new List<(DepotModel depot, double distKm, int mask)>();
        int covered = 0;
        var remaining = candidates.ToList();

        while (remaining.Count > 0 && covered != fullMask)
        {
            // Chọn kho bổ sung nhiều supplies mới nhất, tiebreaker gần nhất
            var best = remaining
                .OrderByDescending(x => BitCount(x.mask & ~covered))
                .ThenBy(x => x.distKm)
                .First();

            if (BitCount(best.mask & ~covered) == 0) break; // không kho nào thêm được gì mới

            chosen.Add(best);
            covered |= best.mask;
            remaining.Remove(best);
        }

        return chosen;
    }

    private static int BitCount(int x)
    {
        int count = 0;
        while (x != 0) { count += x & 1; x >>= 1; }
        return count;
    }

    private static DepotSummary MapToDepotSummary(DepotModel depot, double distKm) => new()
    {
        Id = depot.Id,
        Name = depot.Name,
        Address = depot.Address,
        Latitude = depot.Location!.Latitude,
        Longitude = depot.Location.Longitude,
        DistanceKm = distKm,
        Capacity = depot.Capacity,
        CurrentUtilization = depot.CurrentUtilization,
        Status = depot.Status.ToString(),
        Inventories = depot.InventoryLines
            .Select(l => new DepotInventoryItemDto
            {
                ItemId = l.ReliefItemId,
                ItemName = l.ItemName,
                Unit = l.Unit,
                AvailableQuantity = l.AvailableQuantity
            })
            .ToList()
    };

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
    /// Kiểm tra danh sách tên vật tư đã uppercase có chứa loại vật tư <paramref name="supply"/> không.
    /// Caller phải pre-compute <paramref name="itemNamesUpper"/> một lần cho mỗi kho.
    /// </summary>
    private static bool CoversSingleSupply(
        IReadOnlyList<string> itemNamesUpper,
        string supply)
    {
        if (SupplyCategoryKeywords.TryGetValue(supply, out var keywords))
            return itemNamesUpper.Any(n =>
                keywords.Any(kw => n.Contains(kw, StringComparison.OrdinalIgnoreCase)));

        // Fallback: khớp trực tiếp
        return itemNamesUpper.Any(n => n.Contains(supply, StringComparison.OrdinalIgnoreCase));
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
