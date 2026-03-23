using System.Text.Json;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics;

namespace RESQ.Infrastructure.Services;

public class MissionContextService(
    ISosClusterRepository sosClusterRepository,
    ISosRequestRepository sosRequestRepository,
    IDepotRepository depotRepository,
    ILogger<MissionContextService> logger) : IMissionContextService
{
    private const int MaxDepotContext = 5;
    private const double MaxDepotRadiusKm = 20.0;

    private static readonly Dictionary<string, int> PriorityRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Low"]      = 1,
        ["Medium"]   = 2,
        ["High"]     = 3,
        ["Critical"] = 4
    };

    public async Task<MissionContext> PrepareContextAsync(int clusterId, CancellationToken cancellationToken = default)
    {
        var cluster = await sosClusterRepository.GetByIdAsync(clusterId, cancellationToken);
        if (cluster is null)
            throw new NotFoundException($"Không tìm thấy cluster với ID: {clusterId}");

        var clusterSosRequests = await sosRequestRepository.GetByClusterIdAsync(clusterId, cancellationToken);
        var sosRequestList = clusterSosRequests.ToList();

        if (sosRequestList.Count == 0)
            throw new BadRequestException($"Cluster {clusterId} không có SOS request nào");

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
            CreatedAt = sos.CreatedAt
        }).ToList();

        var neededSupplies = ExtractNeededSupplies(sosRequestSummaries);
        var anchorSos = FindHighestPrioritySos(sosRequestSummaries);

        logger.LogInformation(
            "MissionContext: ClusterId={clusterId}, SOS count={sosCount}, NeededSupplies=[{supplies}], AnchorSOS={sosId}",
            clusterId, sosRequestSummaries.Count, string.Join(", ", neededSupplies), anchorSos?.Id);

        var (nearbyDepots, multiDepotRecommended) = await BuildNearbyDepotSummariesAsync(
            anchorSos, neededSupplies, cancellationToken);

        return new MissionContext
        {
            Cluster = cluster,
            SosRequests = sosRequestSummaries,
            NearbyDepots = nearbyDepots,
            MultiDepotRecommended = multiDepotRecommended
        };
    }

    // ─── Data Preparation Helpers ─────────────────────────────────────────────

    private async Task<(List<DepotSummary> Depots, bool MultiDepotRecommended)> BuildNearbyDepotSummariesAsync(
        SosRequestSummary? anchorSos,
        IReadOnlyCollection<string> neededSupplies,
        CancellationToken cancellationToken)
    {
        if (anchorSos is null)
            throw new BadRequestException(
                "Không lọc được kho phù hợp vì không có SOS request nào có toạ độ GPS. Vui lòng tự lập kế hoạch thủ công.");

        var availableDepots = (await depotRepository.GetAvailableDepotsAsync(cancellationToken)).ToList();

        if (availableDepots.Count == 0)
            throw new BadRequestException(
                "Không lọc được kho phù hợp: hiện không có kho nào đang hoạt động và còn hàng. Vui lòng tự lập kế hoạch thủ công.");

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
            throw new BadRequestException(
                $"Không lọc được kho phù hợp: không có kho nào trong bán kính {MaxDepotRadiusKm} km. Vui lòng tự lập kế hoạch thủ công.");

        var supplyList = neededSupplies.ToList();
        int totalNeeded = supplyList.Count;

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
            .Where(x => totalNeeded == 0 || x.mask > 0)
            .OrderByDescending(x => BitCount(x.mask))
            .ThenBy(x => x.distKm)
            .Take(MaxDepotContext)
            .ToList();

        if (candidates.Count == 0)
            throw new BadRequestException(
                $"Không lọc được kho phù hợp: các kho trong bán kính {MaxDepotRadiusKm} km không có vật tư nào khớp với nhu cầu SOS. Vui lòng tự lập kế hoạch thủ công.");

        if (totalNeeded == 0)
        {
            var nearest = candidates.First();
            return ([MapToDepotSummary(nearest.depot, nearest.distKm)], false);
        }

        int fullMask = (1 << totalNeeded) - 1;
        var bestCover = FindMinimumSetCover(candidates, fullMask);

        bool multiDepotRecommended;
        List<(DepotModel depot, double distKm, int mask)> chosen;

        if (bestCover is not null)
        {
            if (bestCover.Count > 1)
            {
                // Multiple depots needed to cover all supply types
                chosen = bestCover;
                multiDepotRecommended = true;
            }
            else
            {
                // Single depot covers all supply types — but check if it has enough QUANTITY.
                // If other nearby depots have significant additional stock (>= 30% of primary's
                // total available), the primary depot may not have sufficient quantities for
                // all SOS needs, so expose all candidates and recommend multi-depot.
                var primary = bestCover[0];
                if (HasSignificantAlternativeStock(primary, candidates))
                {
                    chosen = candidates; // expose all candidates so AI can draw from any
                    multiDepotRecommended = true;
                    logger.LogInformation(
                        "MissionContext: Single depot (Id={depotId}) covers all supply types but " +
                        "other candidates hold significant additional stock — switching to multi-depot mode.",
                        primary.depot.Id);
                }
                else
                {
                    chosen = bestCover;
                    multiDepotRecommended = false;
                }
            }
        }
        else
        {
            chosen = GreedyMaxCover(candidates, fullMask);
            multiDepotRecommended = true;
        }

        return (chosen.Select(x => MapToDepotSummary(x.depot, x.distKm)).ToList(), multiDepotRecommended);
    }

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
            catch (JsonException) { /* ignore invalid StructuredData */ }
        }
        return needed;
    }

    private static SosRequestSummary? FindHighestPrioritySos(List<SosRequestSummary> sosRequests) =>
        sosRequests
            .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
            .OrderByDescending(s => PriorityRank.TryGetValue(s.PriorityLevel ?? string.Empty, out var rank) ? rank : 0)
            .ThenBy(s => s.CreatedAt ?? DateTime.MaxValue)
            .FirstOrDefault();

    // ─── Set Cover Algorithms ─────────────────────────────────────────────────

    private static List<(DepotModel depot, double distKm, int mask)>? FindMinimumSetCover(
        List<(DepotModel depot, double distKm, int mask)> candidates, int fullMask)
    {
        int n = candidates.Count;
        for (int size = 1; size <= n; size++)
        {
            var best = EnumerateSubsets(candidates, n, size, fullMask);
            if (best is not null) return best;
        }
        return null;
    }

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

    private static List<(DepotModel depot, double distKm, int mask)> GreedyMaxCover(
        List<(DepotModel depot, double distKm, int mask)> candidates, int fullMask)
    {
        var chosen = new List<(DepotModel depot, double distKm, int mask)>();
        int covered = 0;
        var remaining = candidates.ToList();

        while (remaining.Count > 0 && covered != fullMask)
        {
            var best = remaining
                .OrderByDescending(x => BitCount(x.mask & ~covered))
                .ThenBy(x => x.distKm)
                .First();

            if (BitCount(best.mask & ~covered) == 0) break;

            chosen.Add(best);
            covered |= best.mask;
            remaining.Remove(best);
        }
        return chosen;
    }

    // ─── Static Utilities ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when other candidate depots collectively hold stock that is
    /// significant enough to supplement the primary depot.
    /// Threshold: other depots combined have >= 30% of the primary depot's
    /// total available quantity across all its inventory lines.
    /// This catches the case where one depot covers all supply TYPES (by bitmask)
    /// but may not have sufficient QUANTITY — prompting multi-depot mode so the
    /// system exposes all candidates to the AI for quantity-aware planning.
    /// </summary>
    private static bool HasSignificantAlternativeStock(
        (DepotModel depot, double distKm, int mask) primary,
        List<(DepotModel depot, double distKm, int mask)> allCandidates)
    {
        if (allCandidates.Count <= 1) return false;

        var primaryTotal = primary.depot.InventoryLines.Sum(l => l.AvailableQuantity);
        var othersTotal  = allCandidates
            .Where(c => c.depot.Id != primary.depot.Id)
            .Sum(c => c.depot.InventoryLines.Sum(l => l.AvailableQuantity));

        if (primaryTotal == 0) return othersTotal > 0;

        // Recommend multi-depot when other depots hold >= 30% of primary's stock
        return othersTotal >= primaryTotal * 0.30;
    }

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

    private static bool CoversSingleSupply(IReadOnlyList<string> itemNamesUpper, string supply)
    {
        if (SupplyCategoryKeywords.TryGetValue(supply, out var keywords))
            return itemNamesUpper.Any(n => keywords.Any(kw => n.Contains(kw, StringComparison.OrdinalIgnoreCase)));
        return itemNamesUpper.Any(n => n.Contains(supply, StringComparison.OrdinalIgnoreCase));
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
                ItemId = l.ItemModelId,
                ItemName = l.ItemName,
                Unit = l.Unit,
                AvailableQuantity = l.AvailableQuantity
            })
            .ToList()
    };

    private static int BitCount(int x)
    {
        int count = 0;
        while (x != 0) { count += x & 1; x >>= 1; }
        return count;
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double degrees) => degrees * Math.PI / 180.0;
}
