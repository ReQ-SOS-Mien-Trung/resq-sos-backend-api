using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetClosureTransferSuggestions;

public class GetClosureTransferSuggestionsHandler : IRequestHandler<GetClosureTransferSuggestionsQuery, ClosureTransferSuggestionsResponse>
{
    private readonly IDepotRepository _depotRepository;

    public GetClosureTransferSuggestionsHandler(IDepotRepository depotRepository)
    {
        _depotRepository = depotRepository;
    }

    public async Task<ClosureTransferSuggestionsResponse> Handle(GetClosureTransferSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var sourceDepot = await _depotRepository.GetByIdAsync(request.DepotId, cancellationToken);
        if (sourceDepot == null)
            throw new NotFoundException($"Không tìm thấy kho có ID = {request.DepotId}");

        var inventoryItems = await _depotRepository.GetDetailedInventoryForClosureAsync(request.DepotId, cancellationToken);
        var availableDepots = (await _depotRepository.GetAvailableDepotsAsync(cancellationToken)).ToList();

        var targetDepots = availableDepots
            .Where(d => d.Id != request.DepotId)
            .Select(d => BuildCandidateDepotState(sourceDepot, d))
            .Where(d => d.InitialRemainingVolume > 0 && d.InitialRemainingWeight > 0)
            .ToList();

        var transferableItems = inventoryItems
            .Where(x => x.TransferableQuantity > 0)
            .Select(item => BuildSuggestionItemWork(item, targetDepots))
            .OrderBy(item => item.FullFitCandidateCount)
            .ThenByDescending(item => item.TotalFootprintScore)
            .ThenBy(item => item.ItemType.Equals("Reusable", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ItemModelId)
            .ToList();

        var response = new ClosureTransferSuggestionsResponse
        {
            SourceDepotId = sourceDepot.Id,
            SourceDepotName = sourceDepot.Name,
            RecommendationStrategy =
                "Ưu tiên xếp các dòng hàng khó phân bổ trước, cố gắng gom trọn mỗi dòng hàng vào ít kho đích nhất có thể, " +
                "ưu tiên tái sử dụng kho đã được chọn để giảm đầu mối phối hợp, sau đó mới ưu tiên khoảng cách gần và độ khớp sức chứa."
        };

        foreach (var item in transferableItems)
        {
            response.TotalVolumeToTransfer += item.TotalVolume;
            response.TotalWeightToTransfer += item.TotalWeight;

            AllocateItem(item, targetDepots, response);
        }

        ApplyRecommendationRanks(targetDepots, response.SuggestedTransfers);

        response.SuggestedTargetDepotCount = targetDepots.Count(x => x.UsedInPlan);
        response.UnallocatedItemLineCount = response.SuggestedTransfers.Count(x => x.TargetDepotId == null);
        response.SuggestedTransfers = response.SuggestedTransfers
            .OrderBy(x => x.TargetDepotId == null ? 1 : 0)
            .ThenBy(x => x.RecommendationRank == 0 ? int.MaxValue : x.RecommendationRank)
            .ThenBy(x => x.TargetDepotId)
            .ThenBy(x => x.ItemType.Equals("Reusable", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x.ItemName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ItemModelId)
            .ToList();

        response.TargetDepotMetrics = targetDepots
            .OrderBy(x => x.RecommendationRank == 0 ? 1 : 0)
            .ThenBy(x => x.RecommendationRank == 0 ? int.MaxValue : x.RecommendationRank)
            .ThenBy(x => DistanceSortKey(x.DistanceKm))
            .ThenBy(x => x.DepotId)
            .Select(MapTargetDepotMetrics)
            .ToList();

        return response;
    }

    private static void AllocateItem(
        SuggestionItemWork item,
        List<CandidateDepotState> targetDepots,
        ClosureTransferSuggestionsResponse response)
    {
        var singleTarget = targetDepots
            .Where(target => target.CanFit(item.TransferableQuantity, item.VolumePerUnit, item.WeightPerUnit))
            .OrderByDescending(target => target.UsedInPlan)
            .ThenBy(target => DistanceSortKey(target.DistanceKm))
            .ThenBy(target => CalculateProjectedSlackScore(target, item.TotalVolume, item.TotalWeight))
            .ThenBy(target => target.DepotId)
            .FirstOrDefault();

        if (singleTarget != null)
        {
            AddPlannedTransfer(
                response,
                singleTarget,
                item,
                item.TransferableQuantity,
                singleTarget.UsedInPlan ? "Consolidated" : "FullFitSingleDepot");
            return;
        }

        var remainingQty = item.TransferableQuantity;
        while (remainingQty > 0)
        {
            var candidate = targetDepots
                .Select(target => BuildSplitCandidate(target, remainingQty, item.VolumePerUnit, item.WeightPerUnit))
                .Where(x => x.FitQuantity > 0)
                .OrderByDescending(x => x.CanFitAllRemaining)
                .ThenByDescending(x => x.FitQuantity)
                .ThenByDescending(x => x.Target.UsedInPlan)
                .ThenBy(x => DistanceSortKey(x.Target.DistanceKm))
                .ThenBy(x => CalculateProjectedSlackScore(x.Target, x.FitVolume, x.FitWeight))
                .ThenBy(x => x.Target.DepotId)
                .FirstOrDefault();

            if (candidate == null)
            {
                break;
            }

            AddPlannedTransfer(
                response,
                candidate.Target,
                item,
                candidate.FitQuantity,
                "SplitByCapacity");

            remainingQty -= candidate.FitQuantity;
        }

        if (remainingQty > 0)
        {
            var unallocatedVolume = remainingQty * item.VolumePerUnit;
            var unallocatedWeight = remainingQty * item.WeightPerUnit;

            response.UnallocatedVolume += unallocatedVolume;
            response.UnallocatedWeight += unallocatedWeight;
            response.SuggestedTransfers.Add(new TransferSuggestionItemDto
            {
                TargetDepotId = null,
                TargetDepotName = null,
                ItemModelId = item.ItemModelId,
                ItemName = item.ItemName,
                ItemType = item.ItemType,
                Unit = item.Unit,
                SuggestedQuantity = remainingQty,
                TotalVolume = unallocatedVolume,
                TotalWeight = unallocatedWeight,
                AllocationMode = "Unallocated"
            });
        }
    }

    private static void AddPlannedTransfer(
        ClosureTransferSuggestionsResponse response,
        CandidateDepotState target,
        SuggestionItemWork item,
        int quantity,
        string allocationMode)
    {
        var volume = quantity * item.VolumePerUnit;
        var weight = quantity * item.WeightPerUnit;

        target.ApplyPlan(quantity, volume, weight);

        response.SuggestedTransfers.Add(new TransferSuggestionItemDto
        {
            TargetDepotId = target.DepotId,
            TargetDepotName = target.DepotName,
            ItemModelId = item.ItemModelId,
            ItemName = item.ItemName,
            ItemType = item.ItemType,
            Unit = item.Unit,
            SuggestedQuantity = quantity,
            TotalVolume = volume,
            TotalWeight = weight,
            DistanceKm = target.DistanceKm,
            AllocationMode = allocationMode
        });
    }

    private static SplitCandidate BuildSplitCandidate(
        CandidateDepotState target,
        int remainingQty,
        decimal volumePerUnit,
        decimal weightPerUnit)
    {
        var fitQuantity = target.GetFitQuantity(remainingQty, volumePerUnit, weightPerUnit);
        return new SplitCandidate(
            target,
            fitQuantity,
            fitQuantity >= remainingQty,
            fitQuantity * volumePerUnit,
            fitQuantity * weightPerUnit);
    }

    private static CandidateDepotState BuildCandidateDepotState(DepotModel sourceDepot, DepotModel targetDepot)
    {
        double? distanceKm = null;
        if (sourceDepot.Location != null && targetDepot.Location != null)
        {
            distanceKm = Math.Round(
                HaversineKm(
                    sourceDepot.Location.Latitude,
                    sourceDepot.Location.Longitude,
                    targetDepot.Location.Latitude,
                    targetDepot.Location.Longitude),
                2);
        }

        var remainingVolume = Math.Max(0m, targetDepot.Capacity - targetDepot.CurrentUtilization);
        var remainingWeight = Math.Max(0m, targetDepot.WeightCapacity - targetDepot.CurrentWeightUtilization);

        return new CandidateDepotState(
            targetDepot.Id,
            targetDepot.Name,
            targetDepot.Capacity,
            targetDepot.WeightCapacity,
            targetDepot.CurrentUtilization,
            targetDepot.CurrentWeightUtilization,
            remainingVolume,
            remainingWeight,
            distanceKm);
    }

    private static SuggestionItemWork BuildSuggestionItemWork(
        ClosureInventoryItemDto item,
        IReadOnlyCollection<CandidateDepotState> targetDepots)
    {
        var volumePerUnit = ResolveVolumePerUnit(item);
        var weightPerUnit = ResolveWeightPerUnit(item);
        var transferableQuantity = item.TransferableQuantity;
        var totalVolume = transferableQuantity * volumePerUnit;
        var totalWeight = transferableQuantity * weightPerUnit;

        var fullFitCandidateCount = targetDepots.Count(target =>
            target.CanFit(transferableQuantity, volumePerUnit, weightPerUnit));

        return new SuggestionItemWork(
            item.ItemModelId,
            item.ItemName,
            item.ItemType,
            item.Unit,
            transferableQuantity,
            volumePerUnit,
            weightPerUnit,
            totalVolume,
            totalWeight,
            totalVolume + totalWeight,
            fullFitCandidateCount);
    }

    private static void ApplyRecommendationRanks(
        List<CandidateDepotState> targetDepots,
        List<TransferSuggestionItemDto> suggestedTransfers)
    {
        var rankedTargets = targetDepots
            .Where(x => x.UsedInPlan)
            .OrderByDescending(x => x.SuggestedItemLineCount)
            .ThenByDescending(x => x.SuggestedUnitCount)
            .ThenBy(x => DistanceSortKey(x.DistanceKm))
            .ThenBy(x => x.DepotId)
            .ToList();

        for (var i = 0; i < rankedTargets.Count; i++)
        {
            rankedTargets[i].RecommendationRank = i + 1;
        }

        var rankLookup = rankedTargets.ToDictionary(x => x.DepotId, x => x.RecommendationRank);
        foreach (var transfer in suggestedTransfers.Where(x => x.TargetDepotId.HasValue))
        {
            transfer.RecommendationRank = rankLookup.GetValueOrDefault(transfer.TargetDepotId!.Value);
        }
    }

    private static TargetDepotMetricsDto MapTargetDepotMetrics(CandidateDepotState target)
    {
        return new TargetDepotMetricsDto
        {
            DepotId = target.DepotId,
            DepotName = target.DepotName,
            Capacity = target.Capacity,
            WeightCapacity = target.WeightCapacity,
            CurrentUtilization = target.CurrentUtilization,
            CurrentWeightUtilization = target.CurrentWeightUtilization,
            RemainingVolume = target.InitialRemainingVolume,
            RemainingWeight = target.InitialRemainingWeight,
            DistanceKm = target.DistanceKm,
            RecommendationRank = target.RecommendationRank,
            SuggestedItemLineCount = target.SuggestedItemLineCount,
            SuggestedUnitCount = target.SuggestedUnitCount,
            PlannedVolume = target.PlannedVolume,
            PlannedWeight = target.PlannedWeight,
            ProjectedRemainingVolume = target.RemainingVolume,
            ProjectedRemainingWeight = target.RemainingWeight,
            RecommendationReason = BuildRecommendationReason(target)
        };
    }

    private static string BuildRecommendationReason(CandidateDepotState target)
    {
        if (!target.UsedInPlan)
        {
            return target.DistanceKm.HasValue
                ? $"Kho vẫn khả dụng nhưng không được chọn trong phương án hiện tại do kém ưu tiên hơn về khoảng cách hoặc mức độ gom chuyến ({target.DistanceKm.Value:0.##} km)."
                : "Kho vẫn khả dụng nhưng không được chọn trong phương án hiện tại do kém ưu tiên hơn về mức độ gom chuyến.";
        }

        var consolidation = target.SuggestedItemLineCount > 1
            ? $"gom {target.SuggestedItemLineCount} dòng hàng"
            : "nhận gọn 1 dòng hàng";
        var distance = target.DistanceKm.HasValue
            ? $"cách kho nguồn {target.DistanceKm.Value:0.##} km"
            : "không có dữ liệu khoảng cách";

        return $"Được xếp hạng #{target.RecommendationRank} vì có thể {consolidation}, {distance}, và vẫn còn dư sức chứa sau phân bổ.";
    }

    private static decimal CalculateProjectedSlackScore(CandidateDepotState target, decimal plannedVolume, decimal plannedWeight)
    {
        var projectedVolumeRatio = target.Capacity <= 0
            ? 1m
            : Math.Max(0m, target.RemainingVolume - plannedVolume) / target.Capacity;

        var projectedWeightRatio = target.WeightCapacity <= 0
            ? 1m
            : Math.Max(0m, target.RemainingWeight - plannedWeight) / target.WeightCapacity;

        return projectedVolumeRatio + projectedWeightRatio;
    }

    private static double DistanceSortKey(double? distanceKm)
        => distanceKm ?? double.MaxValue;

    private static decimal ResolveVolumePerUnit(ClosureInventoryItemDto item)
        => item.VolumePerUnit.GetValueOrDefault(0.01m);

    private static decimal ResolveWeightPerUnit(ClosureInventoryItemDto item)
        => item.WeightPerUnit.GetValueOrDefault(0.01m);

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private sealed class CandidateDepotState(
        int depotId,
        string depotName,
        decimal capacity,
        decimal weightCapacity,
        decimal currentUtilization,
        decimal currentWeightUtilization,
        decimal initialRemainingVolume,
        decimal initialRemainingWeight,
        double? distanceKm)
    {
        public int DepotId { get; } = depotId;
        public string DepotName { get; } = depotName;
        public decimal Capacity { get; } = capacity;
        public decimal WeightCapacity { get; } = weightCapacity;
        public decimal CurrentUtilization { get; } = currentUtilization;
        public decimal CurrentWeightUtilization { get; } = currentWeightUtilization;
        public decimal InitialRemainingVolume { get; } = initialRemainingVolume;
        public decimal InitialRemainingWeight { get; } = initialRemainingWeight;
        public double? DistanceKm { get; } = distanceKm;

        public decimal RemainingVolume { get; private set; } = initialRemainingVolume;
        public decimal RemainingWeight { get; private set; } = initialRemainingWeight;
        public int SuggestedItemLineCount { get; private set; }
        public int SuggestedUnitCount { get; private set; }
        public decimal PlannedVolume { get; private set; }
        public decimal PlannedWeight { get; private set; }
        public int RecommendationRank { get; set; }

        public bool UsedInPlan => SuggestedItemLineCount > 0;

        public bool CanFit(int quantity, decimal volumePerUnit, decimal weightPerUnit)
            => GetFitQuantity(quantity, volumePerUnit, weightPerUnit) >= quantity;

        public int GetFitQuantity(int desiredQuantity, decimal volumePerUnit, decimal weightPerUnit)
        {
            if (desiredQuantity <= 0)
            {
                return 0;
            }

            var maxQtyByVolume = volumePerUnit <= 0
                ? desiredQuantity
                : Math.Max(0, (int)(RemainingVolume / volumePerUnit));
            var maxQtyByWeight = weightPerUnit <= 0
                ? desiredQuantity
                : Math.Max(0, (int)(RemainingWeight / weightPerUnit));

            return Math.Min(desiredQuantity, Math.Min(maxQtyByVolume, maxQtyByWeight));
        }

        public void ApplyPlan(int quantity, decimal plannedVolume, decimal plannedWeight)
        {
            RemainingVolume = Math.Max(0m, RemainingVolume - plannedVolume);
            RemainingWeight = Math.Max(0m, RemainingWeight - plannedWeight);
            SuggestedItemLineCount++;
            SuggestedUnitCount += quantity;
            PlannedVolume += plannedVolume;
            PlannedWeight += plannedWeight;
        }
    }

    private sealed record SuggestionItemWork(
        int ItemModelId,
        string ItemName,
        string ItemType,
        string Unit,
        int TransferableQuantity,
        decimal VolumePerUnit,
        decimal WeightPerUnit,
        decimal TotalVolume,
        decimal TotalWeight,
        decimal TotalFootprintScore,
        int FullFitCandidateCount);

    private sealed record SplitCandidate(
        CandidateDepotState Target,
        int FitQuantity,
        bool CanFitAllRemaining,
        decimal FitVolume,
        decimal FitWeight);
}
