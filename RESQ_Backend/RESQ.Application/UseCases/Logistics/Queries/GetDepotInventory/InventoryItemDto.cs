using System.Text.Json.Serialization;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotInventory;

public class InventoryItemDto
{
    public int ItemModelId { get; set; }
    public string ItemModelName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public decimal? WeightPerUnit { get; set; }
    public decimal? VolumePerUnit { get; set; }
    public List<string> TargetGroups { get; set; } = new();

    // ── Consumable only (null khi ItemType = Reusable) ────────────────────────────
    /// <summary>Tổng số lượng. Chỉ có với Consumable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Quantity { get; set; }

    /// <summary>Tổng số lượng đang được giữ (mission + transfer). Chỉ có với Consumable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalReservedQuantity { get; set; }

    /// <summary>Số lượng đang giữ cho nhiệm vụ. Chỉ có với Consumable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ReservedForMissionQuantity { get; set; }

    /// <summary>Số lượng đang giữ cho tiếp tế giữa kho. Chỉ có với Consumable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ReservedForTransferQuantity { get; set; }

    /// <summary>Số lượng khả dụng. Chỉ có với Consumable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AvailableQuantity { get; set; }

    // ── Reusable only (null khi ItemType = Consumable) ────────────────────────────
    /// <summary>Tổng số đơn vị. Chỉ có với Reusable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Unit { get; set; }

    /// <summary>Tổng số đơn vị đang bị giữ (mission + transfer reserved). Chỉ có với Reusable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalReservedUnit { get; set; }

    /// <summary>Số đơn vị đang giữ cho nhiệm vụ. Chỉ có với Reusable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ReservedForMissionUnit { get; set; }

    /// <summary>Số đơn vị đang giữ cho tiếp tế giữa kho. Chỉ có với Reusable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ReservedForTransferUnit { get; set; }

    /// <summary>Số đơn vị khả dụng. Chỉ có với Reusable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AvailableUnit { get; set; }

    public DateTime? LastStockedAt { get; set; }

    // ── Lot summary (Consumable only, null khi Reusable) ──
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? NearestExpiryDate { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LotCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsExpiringSoon { get; set; }

    /// <summary>
    /// Chi tiết trạng thái và tình trạng cho vật phẩm tái sử dụng.
    /// Null với ItemType = Consumable.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReusableBreakdownDto? ReusableBreakdown { get; set; }
}

/// <summary>Chi tiết thống kê trạng thái + tình trạng vật phẩm tái sử dụng.</summary>
public class ReusableBreakdownDto
{
    public int TotalUnits { get; set; }
    public int AvailableUnits { get; set; }
    /// <summary>Tổng đơn vị đã được đặt trữ (nhiệm vụ + chuyển kho).</summary>
    public int TotalReservedUnits { get; set; }
    /// <summary>Đơn vị đặt trữ cho nhiệm vụ (chưa xuất kho).</summary>
    public int ReservedForMissionUnits { get; set; }
    /// <summary>Đơn vị đặt trữ cho tiếp tế giữa kho (chưa xuất kho).</summary>
    public int ReservedForTransferUnits { get; set; }
    /// <summary>Đơn vị đang trên đường vận chuyển.</summary>
    public int InTransitUnits { get; set; }
    public int InUseUnits { get; set; }
    public int MaintenanceUnits { get; set; }
    public int DecommissionedUnits { get; set; }
    public int GoodCount { get; set; }
    public int FairCount { get; set; }
    public int PoorCount { get; set; }
}

