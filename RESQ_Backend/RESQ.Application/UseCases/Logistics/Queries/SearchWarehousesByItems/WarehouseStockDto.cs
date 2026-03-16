namespace RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;

/// <summary>
/// Represents a single depot's stock level for a specific relief item.
/// </summary>
public class WarehouseStockDto
{
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public string DepotAddress { get; set; } = string.Empty;
    public string DepotStatus { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public DateTime? LastStockedAt { get; set; }
    /// <summary>
    /// Straight-line distance in kilometres from the requesting manager's depot.
    /// Null when the manager's depot has no location data.
    /// </summary>
    public double? DistanceKm { get; set; }
}
