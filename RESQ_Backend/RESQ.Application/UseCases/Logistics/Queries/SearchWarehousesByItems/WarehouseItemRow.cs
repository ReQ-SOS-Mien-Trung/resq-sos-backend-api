namespace RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;

/// <summary>
/// Flat row returned by the repository - one row per (item, depot) combination.
/// The handler groups these into the hierarchical DTO.
/// </summary>
public class WarehouseItemRow
{
    public int ItemModelId { get; set; }
    public string ItemModelName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public string? Unit { get; set; }

    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public string DepotAddress { get; set; } = string.Empty;
    public string DepotStatus { get; set; } = string.Empty;
    public double? DepotLatitude { get; set; }
    public double? DepotLongitude { get; set; }
    public int TotalQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public DateTime? LastStockedAt { get; set; }

    // -- Reusable-only fields (0 for Consumable) ------------------------------
    /// <summary>Available units with Condition = "Good". Only populated for Reusable items.</summary>
    public int GoodAvailableCount { get; set; }
    /// <summary>Available units with Condition = "Fair". Only populated for Reusable items.</summary>
    public int FairAvailableCount { get; set; }
    /// <summary>Available units with Condition = "Poor". Only populated for Reusable items.</summary>
    public int PoorAvailableCount { get; set; }
}
