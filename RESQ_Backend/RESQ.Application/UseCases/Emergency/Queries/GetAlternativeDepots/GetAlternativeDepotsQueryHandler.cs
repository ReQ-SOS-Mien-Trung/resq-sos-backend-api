using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Emergency.Shared;
using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.UseCases.Emergency.Queries.GetAlternativeDepots;

public class GetAlternativeDepotsQueryHandler(
    ISosClusterRepository sosClusterRepository,
    IMissionAiSuggestionRepository missionAiSuggestionRepository,
    IDepotRepository depotRepository,
    ILogger<GetAlternativeDepotsQueryHandler> logger)
    : IRequestHandler<GetAlternativeDepotsQuery, GetAlternativeDepotsResponse>
{
    private const int MaxAlternativeDepotCount = 3;
    private const string CoverageStatusFull = "Full";
    private const string CoverageStatusPartial = "Partial";
    private const string CoverageStatusNone = "None";

    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly IMissionAiSuggestionRepository _missionAiSuggestionRepository = missionAiSuggestionRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly ILogger<GetAlternativeDepotsQueryHandler> _logger = logger;

    public async Task<GetAlternativeDepotsResponse> Handle(
        GetAlternativeDepotsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SelectedDepotId <= 0)
            throw new BadRequestException("selectedDepotId phải lớn hơn 0.");

        _logger.LogInformation(
            "Getting alternative depots for ClusterId={clusterId}, SelectedDepotId={selectedDepotId}",
            request.ClusterId,
            request.SelectedDepotId);

        var cluster = await _sosClusterRepository.GetByIdAsync(request.ClusterId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy cụm với ID: {request.ClusterId}");

        if (!cluster.CenterLatitude.HasValue || !cluster.CenterLongitude.HasValue)
            throw new BadRequestException($"Cụm {request.ClusterId} không có tọa độ tâm để xếp hạng kho thay thế.");

        var latestSuggestion = (await _missionAiSuggestionRepository.GetByClusterIdAsync(request.ClusterId, cancellationToken))
            .OrderByDescending(suggestion => suggestion.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(suggestion => suggestion.Id)
            .FirstOrDefault();

        if (latestSuggestion is null)
            throw new NotFoundException($"Cụm {request.ClusterId} chưa có gợi ý nhiệm vụ từ AI.");

        var metadata = MissionAiSuggestionJsonHelper.ParseMetadata(latestSuggestion.Metadata);
        var rawShortages = metadata?.SupplyShortages ?? [];

        if (rawShortages.Any(shortage =>
                shortage.SelectedDepotId.HasValue
                && shortage.SelectedDepotId.Value != request.SelectedDepotId))
        {
            throw new BadRequestException(
                $"selectedDepotId={request.SelectedDepotId} không khớp với kho chính trong gợi ý AI mới nhất của cụm {request.ClusterId}.");
        }

        var aggregatedShortages = AggregateShortages(rawShortages);
        var totalMissingQuantity = aggregatedShortages.Sum(shortage => shortage.MissingQuantity);

        if (aggregatedShortages.Count == 0)
        {
            return CreateEmptyResponse(request.ClusterId, request.SelectedDepotId, latestSuggestion.Id);
        }

        var candidateDepots = (await _depotRepository.GetAvailableDepotsAsync(cancellationToken))
            .Where(depot => depot.Id != request.SelectedDepotId && depot.Location is not null)
            .Select(depot => BuildCandidateDepotDto(
                depot,
                aggregatedShortages,
                cluster.CenterLatitude.Value,
                cluster.CenterLongitude.Value,
                totalMissingQuantity))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .OrderByDescending(candidate => candidate.CoversAllShortages)
            .ThenByDescending(candidate => candidate.CoveredQuantity)
            .ThenBy(candidate => candidate.DistanceKm)
            .ThenBy(candidate => candidate.DepotId)
            .Take(MaxAlternativeDepotCount)
            .ToList();

        return new GetAlternativeDepotsResponse
        {
            ClusterId = request.ClusterId,
            SelectedDepotId = request.SelectedDepotId,
            SourceSuggestionId = latestSuggestion.Id,
            TotalShortageItems = aggregatedShortages.Count,
            TotalMissingQuantity = totalMissingQuantity,
            AlternativeDepots = candidateDepots
        };
    }

    private static GetAlternativeDepotsResponse CreateEmptyResponse(int clusterId, int selectedDepotId, int sourceSuggestionId)
    {
        return new GetAlternativeDepotsResponse
        {
            ClusterId = clusterId,
            SelectedDepotId = selectedDepotId,
            SourceSuggestionId = sourceSuggestionId,
            TotalShortageItems = 0,
            TotalMissingQuantity = 0,
            AlternativeDepots = []
        };
    }

    private static List<AggregatedShortageItem> AggregateShortages(IEnumerable<RESQ.Application.Services.SupplyShortageDto> shortages)
    {
        return shortages
            .Select(AggregatedShortageItem.From)
            .Where(shortage => shortage.MissingQuantity > 0)
            .GroupBy(shortage => shortage.GroupKey, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                var displayName = group
                    .Select(item => item.ItemName)
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                    ?? first.ItemName;
                var displayUnit = group
                    .Select(item => item.Unit)
                    .FirstOrDefault(unit => !string.IsNullOrWhiteSpace(unit))
                    ?? first.Unit;

                return first with
                {
                    ItemName = string.IsNullOrWhiteSpace(displayName)
                        ? BuildFallbackItemName(first.ItemId)
                        : displayName,
                    Unit = displayUnit,
                    MissingQuantity = group.Sum(item => item.MissingQuantity)
                };
            })
            .OrderBy(shortage => shortage.ItemId ?? int.MaxValue)
            .ThenBy(shortage => shortage.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AlternativeDepotDto? BuildCandidateDepotDto(
        DepotModel depot,
        IReadOnlyList<AggregatedShortageItem> shortages,
        double clusterLatitude,
        double clusterLongitude,
        int totalMissingQuantity)
    {
        var itemCoverageDetails = shortages
            .Select(shortage => BuildCoverageDetail(shortage, depot.InventoryLines))
            .ToList();

        var coveredQuantity = itemCoverageDetails.Sum(detail => detail.CoveredQuantity);
        if (coveredQuantity <= 0)
            return null;

        var distanceKm = Math.Round(
            HaversineKm(
                clusterLatitude,
                clusterLongitude,
                depot.Location!.Latitude,
                depot.Location.Longitude),
            2);

        var coversAllShortages = itemCoverageDetails.All(detail =>
            string.Equals(detail.CoverageStatus, CoverageStatusFull, StringComparison.Ordinal));

        return new AlternativeDepotDto
        {
            DepotId = depot.Id,
            DepotName = depot.Name,
            DepotAddress = depot.Address,
            Latitude = depot.Location.Latitude,
            Longitude = depot.Location.Longitude,
            DistanceKm = distanceKm,
            CoversAllShortages = coversAllShortages,
            CoveredQuantity = coveredQuantity,
            TotalMissingQuantity = totalMissingQuantity,
            CoveragePercent = totalMissingQuantity == 0
                ? 0
                : Math.Round((double)coveredQuantity / totalMissingQuantity, 4),
            Reason = BuildReason(coversAllShortages, coveredQuantity, totalMissingQuantity, distanceKm, itemCoverageDetails),
            ItemCoverageDetails = itemCoverageDetails
        };
    }

    private static AlternativeDepotItemCoverageDto BuildCoverageDetail(
        AggregatedShortageItem shortage,
        IReadOnlyList<DepotInventoryLine> inventoryLines)
    {
        var availableQuantity = shortage.ItemId.HasValue
            ? inventoryLines
                .Where(line => line.ItemModelId == shortage.ItemId.Value)
                .Sum(line => Math.Max(line.AvailableQuantity, 0))
            : inventoryLines
                .Where(line => MatchesByNormalizedName(shortage, line))
                .Sum(line => Math.Max(line.AvailableQuantity, 0));

        var coveredQuantity = Math.Min(availableQuantity, shortage.MissingQuantity);
        var remainingQuantity = Math.Max(shortage.MissingQuantity - coveredQuantity, 0);

        return new AlternativeDepotItemCoverageDto
        {
            ItemId = shortage.ItemId,
            ItemName = shortage.ItemName,
            Unit = shortage.Unit,
            NeededQuantity = shortage.MissingQuantity,
            AvailableQuantity = availableQuantity,
            CoveredQuantity = coveredQuantity,
            RemainingQuantity = remainingQuantity,
            CoverageStatus = coveredQuantity <= 0
                ? CoverageStatusNone
                : coveredQuantity >= shortage.MissingQuantity
                    ? CoverageStatusFull
                    : CoverageStatusPartial
        };
    }

    private static bool MatchesByNormalizedName(AggregatedShortageItem shortage, DepotInventoryLine line)
    {
        var shortageName = shortage.NormalizedItemName;
        var inventoryName = NormalizeText(line.ItemName);

        if (string.IsNullOrWhiteSpace(shortageName) || string.IsNullOrWhiteSpace(inventoryName))
            return false;

        var nameMatches = shortageName.Equals(inventoryName, StringComparison.Ordinal)
            || shortageName.Contains(inventoryName, StringComparison.Ordinal)
            || inventoryName.Contains(shortageName, StringComparison.Ordinal);

        if (!nameMatches)
            return false;

        if (string.IsNullOrWhiteSpace(shortage.NormalizedUnit))
            return true;

        var inventoryUnit = NormalizeText(line.Unit);
        return string.IsNullOrWhiteSpace(inventoryUnit)
            || inventoryUnit.Equals(shortage.NormalizedUnit, StringComparison.Ordinal);
    }

    private static string BuildReason(
        bool coversAllShortages,
        int coveredQuantity,
        int totalMissingQuantity,
        double distanceKm,
        IReadOnlyCollection<AlternativeDepotItemCoverageDto> itemCoverageDetails)
    {
        var distanceLabel = distanceKm.ToString("0.##", CultureInfo.InvariantCulture);
        if (coversAllShortages)
        {
            return $"Kho này đáp ứng toàn bộ phần thiếu hụt ({coveredQuantity}/{totalMissingQuantity} đơn vị), cách tâm cụm {distanceLabel} km.";
        }

        var remainingItems = itemCoverageDetails
            .Where(detail => detail.RemainingQuantity > 0)
            .Select(detail => detail.ItemName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missingLabel = remainingItems.Count == 0
            ? "một số mặt hàng"
            : JoinWithAnd(remainingItems);

        return $"Kho này đáp ứng một phần {coveredQuantity}/{totalMissingQuantity} đơn vị, còn thiếu {missingLabel}, cách tâm cụm {distanceLabel} km.";
    }

    private static string JoinWithAnd(IReadOnlyList<string> values)
    {
        return values.Count switch
        {
            0 => string.Empty,
            1 => values[0],
            2 => $"{values[0]} và {values[1]}",
            _ => $"{string.Join(", ", values.Take(values.Count - 1))} và {values[^1]}"
        };
    }

    private static string BuildFallbackItemName(int? itemId)
    {
        return itemId.HasValue ? $"Vật tư #{itemId.Value}" : "Vật tư chưa rõ";
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousWasSpace = false;

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            var folded = ch switch
            {
                '\u0111' => 'd',
                '\u0110' => 'd',
                _ => char.ToLowerInvariant(ch)
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

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusKm = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private sealed record AggregatedShortageItem
    {
        public int? ItemId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public string? Unit { get; init; }
        public int MissingQuantity { get; init; }
        public string GroupKey { get; init; } = string.Empty;
        public string NormalizedItemName { get; init; } = string.Empty;
        public string NormalizedUnit { get; init; } = string.Empty;

        public static AggregatedShortageItem From(RESQ.Application.Services.SupplyShortageDto shortage)
        {
            var normalizedName = NormalizeText(shortage.ItemName);
            var normalizedUnit = NormalizeText(shortage.Unit);
            var groupKey = shortage.ItemId.HasValue
                ? $"item:{shortage.ItemId.Value}"
                : $"name:{normalizedName}|unit:{normalizedUnit}";

            return new AggregatedShortageItem
            {
                ItemId = shortage.ItemId,
                ItemName = string.IsNullOrWhiteSpace(shortage.ItemName)
                    ? BuildFallbackItemName(shortage.ItemId)
                    : shortage.ItemName,
                Unit = shortage.Unit,
                MissingQuantity = Math.Max(shortage.MissingQuantity, 0),
                GroupKey = groupKey,
                NormalizedItemName = normalizedName,
                NormalizedUnit = normalizedUnit
            };
        }
    }
}

