using System.Text.Json;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Infrastructure.Services;

public class MissionContextService(
    ISosClusterRepository sosClusterRepository,
    ISosRequestRepository sosRequestRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    IDepotRepository depotRepository,
    IPersonnelQueryRepository personnelQueryRepository,
    IRescueTeamRadiusConfigRepository rescueTeamRadiusConfigRepository,
    ILogger<MissionContextService> logger) : IMissionContextService
{
    private const int MaxDepotContext = 5;
    private const double MaxDepotRadiusKm = 20.0;
    private const double DefaultMaxTeamRadiusKm = 10.0;

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
        var victimUpdateLookup = await sosRequestUpdateRepository.GetLatestVictimUpdatesBySosRequestIdsAsync(
            sosRequestList.Select(x => x.Id),
            cancellationToken);
        var incidentLookup = await sosRequestUpdateRepository.GetIncidentHistoryBySosRequestIdsAsync(
            sosRequestList.Select(x => x.Id),
            cancellationToken);

        if (sosRequestList.Count == 0)
            throw new BadRequestException($"Cluster {clusterId} không có SOS request nào");

        var effectiveSosRequests = sosRequestList.Select(sos =>
        {
            victimUpdateLookup.TryGetValue(sos.Id, out var latestVictimUpdate);
            return SosRequestVictimUpdateOverlay.Apply(sos, latestVictimUpdate);
        }).ToList();

        var sosRequestSummaries = effectiveSosRequests.Select(sos =>
        {
            incidentLookup.TryGetValue(sos.Id, out var incidentHistory);
            var victimContext = MissionActivityVictimContextHelper.BuildContext(sos.StructuredData, sos.Id);

            return new SosRequestSummary
            {
                Id = sos.Id,
                SosType = sos.SosType,
                RawMessage = sos.RawMessage,
                StructuredData = sos.StructuredData,
                PriorityLevel = sos.PriorityLevel?.ToString(),
                Status = sos.Status.ToString(),
                LatestIncidentNote = incidentHistory?.FirstOrDefault()?.Note,
                IncidentNotes = incidentHistory?.Select(x => x.Note).ToList() ?? [],
                Latitude = sos.Location?.Latitude,
                Longitude = sos.Location?.Longitude,
                CreatedAt = sos.CreatedAt,
                TargetVictimSummary = victimContext.Summary,
                TargetVictims = MissionActivityVictimContextHelper.CloneVictims(victimContext.Victims)
            };
        }).ToList();

        var neededSupplies = ExtractNeededSupplies(sosRequestSummaries);
        var anchorSos = FindHighestPrioritySos(sosRequestSummaries);

        logger.LogInformation(
            "MissionContext: ClusterId={clusterId}, SOS count={sosCount}, NeededSupplies=[{supplies}], AnchorSOS={sosId}",
            clusterId, sosRequestSummaries.Count, string.Join(", ", neededSupplies), anchorSos?.Id);

        var (nearbyDepots, multiDepotRecommended) = await BuildNearbyDepotSummariesAsync(
            anchorSos, neededSupplies, cancellationToken);
        var nearbyTeams = await BuildNearbyTeamSummariesAsync(cluster, cancellationToken);

        return new MissionContext
        {
            Cluster = cluster,
            SosRequests = sosRequestSummaries,
            NearbyDepots = nearbyDepots,
            NearbyTeams = nearbyTeams,
            MultiDepotRecommended = multiDepotRecommended
        };
    }

    // --- Data Preparation Helpers ---------------------------------------------

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
        {
            logger.LogWarning("MissionContext: no active depots available for anchor SOS {sosId}", anchorSos.Id);
            return ([], false);
        }

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
            logger.LogWarning(
                "MissionContext: no nearby depots found within {radiusKm}km for anchor SOS {sosId}",
                MaxDepotRadiusKm,
                anchorSos.Id);
            return ([], false);
        }

        var supplyList = neededSupplies.ToList();
        int totalNeeded = supplyList.Count;

        if (totalNeeded == 0)
        {
            return (nearby
                .OrderBy(x => x.distKm)
                .Take(MaxDepotContext)
                .Select(x => MapToDepotSummary(x.depot, x.distKm))
                .ToList(), false);
        }

        var candidates = nearby
            .Select(x =>
            {
                int mask = 0;
                int matchedQuantity = 0;

                for (int i = 0; i < supplyList.Count; i++)
                {
                    if (x.depot.InventoryLines.Any(line => MatchesSupply(line.ItemName, supplyList[i])))
                        mask |= (1 << i);
                }

                matchedQuantity = x.depot.InventoryLines
                    .Where(line => supplyList.Any(supply => MatchesSupply(line.ItemName, supply)))
                    .Sum(line => line.AvailableQuantity);

                return (x.depot, x.distKm, mask, matchedQuantity);
            })
            .Where(x => x.mask > 0)
            .OrderByDescending(x => BitCount(x.mask))
            .ThenByDescending(x => x.matchedQuantity)
            .ThenBy(x => x.distKm)
            .Take(MaxDepotContext)
            .ToList();

        if (candidates.Count == 0)
        {
            logger.LogInformation(
                "MissionContext: nearby depots exist for anchor SOS {sosId} but none currently match needed supplies [{supplies}]",
                anchorSos.Id,
                string.Join(", ", supplyList));

            return (nearby
                .OrderBy(x => x.distKm)
                .Take(MaxDepotContext)
                .Select(x => MapToDepotSummary(x.depot, x.distKm))
                .ToList(), false);
        }

        return (candidates
            .Select(x => MapToDepotSummary(x.depot, x.distKm))
            .ToList(), false);
    }

    private async Task<List<AgentTeamInfo>> BuildNearbyTeamSummariesAsync(
        RESQ.Domain.Entities.Emergency.SosClusterModel cluster,
        CancellationToken cancellationToken)
    {
        if (!cluster.CenterLatitude.HasValue || !cluster.CenterLongitude.HasValue)
            return [];

        var radiusConfig = await rescueTeamRadiusConfigRepository.GetAsync(cancellationToken);
        var maxRadiusKm = radiusConfig?.MaxRadiusKm ?? DefaultMaxTeamRadiusKm;
        var teams = await personnelQueryRepository.GetAllAvailableTeamsAsync(cancellationToken);

        return teams
            .Where(team => team.AssemblyPointLocation is not null)
            .Select(team => new AgentTeamInfo
            {
                TeamId = team.Id,
                TeamName = team.Name,
                TeamType = team.TeamType.ToString(),
                Status = team.Status.ToString(),
                IsAvailable = true,
                MemberCount = team.Members.Count(member => member.Status != TeamMemberStatus.Removed),
                AssemblyPointId = team.AssemblyPointId,
                AssemblyPointName = team.AssemblyPointName,
                Latitude = team.AssemblyPointLocation?.Latitude,
                Longitude = team.AssemblyPointLocation?.Longitude,
                DistanceKm = Math.Round(
                    HaversineKm(
                        cluster.CenterLatitude.Value,
                        cluster.CenterLongitude.Value,
                        team.AssemblyPointLocation!.Latitude,
                        team.AssemblyPointLocation.Longitude),
                    2)
            })
            .Where(team => team.DistanceKm.HasValue && team.DistanceKm.Value <= maxRadiusKm)
            .OrderBy(team => team.DistanceKm)
            .ThenBy(team => team.TeamName)
            .ToList();
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
                var root = doc.RootElement;

                // Dual-read: try new nested format first, fallback to old flat
                if (root.TryGetProperty("group_needs", out var groupNeeds))
                {
                    // New nested format
                    if (groupNeeds.TryGetProperty("supplies", out var supplies)
                        && supplies.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in supplies.EnumerateArray())
                        {
                            var val = item.GetString();
                            if (!string.IsNullOrWhiteSpace(val))
                                needed.Add(val.Trim().ToUpperInvariant());
                        }
                    }

                    if (groupNeeds.TryGetProperty("water", out var water) && water.ValueKind == JsonValueKind.Object)
                    {
                        if (water.TryGetProperty("duration", out var wd) && wd.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(wd.GetString()))
                            needed.Add("WATER");
                        if (water.TryGetProperty("remaining", out var wr) && wr.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(wr.GetString()))
                            needed.Add("WATER");
                    }

                    if (groupNeeds.TryGetProperty("food", out var food) && food.ValueKind == JsonValueKind.Object)
                    {
                        if (food.TryGetProperty("duration", out var fd) && fd.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(fd.GetString()))
                            needed.Add("FOOD");
                    }

                    if (groupNeeds.TryGetProperty("blanket", out var blanket) && blanket.ValueKind == JsonValueKind.Object)
                    {
                        if (blanket.TryGetProperty("request_count", out var rc) && rc.ValueKind == JsonValueKind.Number && rc.GetInt32() > 0)
                            needed.Add("CLOTHING");
                    }

                    if (groupNeeds.TryGetProperty("medicine", out var medicine) && medicine.ValueKind == JsonValueKind.Object)
                    {
                        if (medicine.TryGetProperty("medical_needs", out var mn) && mn.ValueKind == JsonValueKind.Array && mn.GetArrayLength() > 0)
                            needed.Add("MEDICINE");
                        if (medicine.TryGetProperty("medical_description", out var md) && md.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(md.GetString()))
                            needed.Add("MEDICINE");
                    }

                    if (groupNeeds.TryGetProperty("clothing", out var clothing) && clothing.ValueKind == JsonValueKind.Object)
                    {
                        if (clothing.TryGetProperty("status", out var cs) && cs.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(cs.GetString()))
                            needed.Add("CLOTHING");
                    }
                }
                else
                {
                    // Old flat format
                    if (root.TryGetProperty("supplies", out var supplies)
                        && supplies.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in supplies.EnumerateArray())
                        {
                            var val = item.GetString();
                            if (!string.IsNullOrWhiteSpace(val))
                                needed.Add(val.Trim().ToUpperInvariant());
                        }
                    }

                    if (root.TryGetProperty("supply_details", out var supplyDetails) && supplyDetails.ValueKind == JsonValueKind.Object)
                    {
                        if (supplyDetails.TryGetProperty("are_blankets_enough", out var blanketsEnough) && blanketsEnough.ValueKind == JsonValueKind.False)
                            needed.Add("CLOTHING");
                        if (supplyDetails.TryGetProperty("blanket_request_count", out var blanketCount) && blanketCount.ValueKind == JsonValueKind.Number && blanketCount.GetInt32() > 0)
                            needed.Add("CLOTHING");
                        if (supplyDetails.TryGetProperty("clothing_persons", out var clothingArr) && clothingArr.ValueKind == JsonValueKind.Array && clothingArr.GetArrayLength() > 0)
                            needed.Add("CLOTHING");

                        if (supplyDetails.TryGetProperty("food_duration", out var foodDur) && foodDur.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(foodDur.GetString()))
                            needed.Add("FOOD");
                        if (supplyDetails.TryGetProperty("special_diet_persons", out var dietArr) && dietArr.ValueKind == JsonValueKind.Array && dietArr.GetArrayLength() > 0)
                            needed.Add("FOOD");

                        if (supplyDetails.TryGetProperty("water_duration", out var waterDur) && waterDur.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(waterDur.GetString()))
                            needed.Add("WATER");
                        if (supplyDetails.TryGetProperty("water_remaining", out var waterRem) && waterRem.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(waterRem.GetString()))
                            needed.Add("WATER");

                        if (supplyDetails.TryGetProperty("medical_needs", out var medNeeds) && medNeeds.ValueKind == JsonValueKind.Array && medNeeds.GetArrayLength() > 0)
                            needed.Add("MEDICINE");
                        if (supplyDetails.TryGetProperty("medical_description", out var medDesc) && medDesc.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(medDesc.GetString()))
                            needed.Add("MEDICINE");
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

    // --- Set Cover Algorithms -------------------------------------------------

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

    // --- Static Utilities -----------------------------------------------------

    /// <summary>
    /// Returns true when other candidate depots collectively hold stock that is
    /// significant enough to supplement the primary depot.
    /// Threshold: other depots combined have >= 30% of the primary depot's
    /// total available quantity across all its inventory lines.
    /// This catches the case where one depot covers all supply TYPES (by bitmask)
    /// but may not have sufficient QUANTITY - prompting multi-depot mode so the
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
        return itemNamesUpper.Any(itemName => MatchesSupply(itemName, supply));
    }

    private static bool MatchesSupply(string? itemName, string supply)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return false;

        if (SupplyCategoryKeywords.TryGetValue(supply, out var keywords))
            return keywords.Any(keyword => itemName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        return itemName.Contains(supply, StringComparison.OrdinalIgnoreCase);
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
        WeightCapacity = depot.WeightCapacity,
        CurrentWeightUtilization = depot.CurrentWeightUtilization,
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
