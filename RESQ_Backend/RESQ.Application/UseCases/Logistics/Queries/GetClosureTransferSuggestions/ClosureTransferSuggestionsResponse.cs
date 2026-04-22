namespace RESQ.Application.UseCases.Logistics.Queries.GetClosureTransferSuggestions;

public class ClosureTransferSuggestionsResponse
{
    public int SourceDepotId { get; set; }
    public string SourceDepotName { get; set; } = string.Empty;

    public decimal TotalVolumeToTransfer { get; set; }
    public decimal TotalWeightToTransfer { get; set; }
    
    public decimal UnallocatedVolume { get; set; }
    public decimal UnallocatedWeight { get; set; }
    public int SuggestedTargetDepotCount { get; set; }
    public int UnallocatedItemLineCount { get; set; }
    public string RecommendationStrategy { get; set; } = string.Empty;

    public List<TargetDepotMetricsDto> TargetDepotMetrics { get; set; } = [];
    public List<TransferSuggestionItemDto> SuggestedTransfers { get; set; } = [];
}

public class TargetDepotMetricsDto
{
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public decimal Capacity { get; set; }
    public decimal WeightCapacity { get; set; }
    public decimal CurrentUtilization { get; set; }
    public decimal CurrentWeightUtilization { get; set; }
    public decimal RemainingVolume { get; set; }
    public decimal RemainingWeight { get; set; }
    public double? DistanceKm { get; set; }
    public int RecommendationRank { get; set; }
    public int SuggestedItemLineCount { get; set; }
    public int SuggestedUnitCount { get; set; }
    public decimal PlannedVolume { get; set; }
    public decimal PlannedWeight { get; set; }
    public decimal ProjectedRemainingVolume { get; set; }
    public decimal ProjectedRemainingWeight { get; set; }
    public string RecommendationReason { get; set; } = string.Empty;
}

public class TransferSuggestionItemDto
{
    public int? TargetDepotId { get; set; }
    public string? TargetDepotName { get; set; } = string.Empty;
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int SuggestedQuantity { get; set; }
    public decimal TotalVolume { get; set; }
    public decimal TotalWeight { get; set; }
    public double? DistanceKm { get; set; }
    public int RecommendationRank { get; set; }
    public string AllocationMode { get; set; } = string.Empty;
}
