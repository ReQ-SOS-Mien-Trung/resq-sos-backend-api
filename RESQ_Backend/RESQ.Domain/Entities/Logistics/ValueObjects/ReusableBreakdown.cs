namespace RESQ.Domain.Entities.Logistics.ValueObjects;

/// <summary>
/// Thống kê chi tiết trạng thái và tình trạng của vật phẩm tái sử dụng trong kho.
/// Chỉ áp dụng cho ItemType = Reusable.
/// </summary>
public record ReusableBreakdown
{
    /// <summary>Tổng số đơn vị (bao gồm mọi trạng thái).</summary>
    public int TotalUnits { get; init; }

    /// <summary>Số đơn vị đang sẵn sàng sử dụng.</summary>
    public int AvailableUnits { get; init; }

    /// <summary>
    /// Số đơn vị đã được đặt trước cho nhiệm vụ cứu hộ
    /// (Status == Reserved và SupplyRequestId IS NULL).
    /// </summary>
    public int ReservedForMissionUnits { get; init; }

    /// <summary>
    /// Số đơn vị đã được đặt trước cho tiếp tế giữa kho
    /// (Status == Reserved và SupplyRequestId IS NOT NULL).
    /// </summary>
    public int ReservedForTransferUnits { get; init; }

    /// <summary>Tổng đơn vị được đặt trước = ReservedForMissionUnits + ReservedForTransferUnits.</summary>
    public int TotalReservedUnits => ReservedForMissionUnits + ReservedForTransferUnits;

    /// <summary>Số đơn vị đang trên đường vận chuyển.</summary>
    public int InTransitUnits { get; init; }
    /// <summary>Số đơn vị đang được sử dụng.</summary>
    public int InUseUnits { get; init; }

    /// <summary>Số đơn vị đang bảo trì / sửa chữa.</summary>
    public int MaintenanceUnits { get; init; }

    /// <summary>Số đơn vị đã thanh lý.</summary>
    public int DecommissionedUnits { get; init; }

    /// <summary>Số đơn vị tình trạng Tốt.</summary>
    public int GoodCount { get; init; }

    /// <summary>Số đơn vị tình trạng Trung bình.</summary>
    public int FairCount { get; init; }

    /// <summary>Số đơn vị tình trạng Kém.</summary>
    public int PoorCount { get; init; }
}
