namespace RESQ.Application.UseCases.Emergency.Queries.GetAlternativeDepots;

public class GetAlternativeDepotsResponse
{
    public int ClusterId { get; set; }
    public int SelectedDepotId { get; set; }
    public int SourceSuggestionId { get; set; }
    public int TotalShortageItems { get; set; }
    public int TotalMissingQuantity { get; set; }
    public List<AlternativeDepotDto> AlternativeDepots { get; set; } = [];
}

public class AlternativeDepotDto
{
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public string DepotAddress { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double DistanceKm { get; set; }
    public bool CoversAllShortages { get; set; }
    public int CoveredQuantity { get; set; }
    public int TotalMissingQuantity { get; set; }
    public double CoveragePercent { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<AlternativeDepotItemCoverageDto> ItemCoverageDetails { get; set; } = [];
}

public class AlternativeDepotItemCoverageDto
{
    public int? ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int NeededQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int CoveredQuantity { get; set; }
    public int RemainingQuantity { get; set; }
    public string CoverageStatus { get; set; } = string.Empty;
}
