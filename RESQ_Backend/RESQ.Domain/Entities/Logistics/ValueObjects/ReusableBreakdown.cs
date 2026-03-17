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
