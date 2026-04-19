namespace RESQ.Application.UseCases.Operations.Commands.ConfirmReturnSupplies;

public class ConfirmReturnSuppliesRequestDto
{
    public string? DiscrepancyNote { get; set; }
    public List<ActualReturnedConsumableItemDto> ConsumableItems { get; set; } = [];
    public List<ActualReturnedReusableItemDto> ReusableItems { get; set; } = [];
}

public class ActualReturnedConsumableItemDto
{
    public int ItemModelId { get; set; }
    /// <summary>
    /// Số lượng trả về (fallback khi không dùng lotAllocations).
    /// Nếu gửi cùng lotAllocations, phải khớp tổng quantityTaken của các lot.
    /// </summary>
    public int Quantity { get; set; }
    /// <summary>
    /// Danh sách lot cần trả về. Bắt buộc khi activity yêu cầu xác nhận theo lot.
    /// </summary>
    public List<ConfirmReturnLotAllocationDto>? LotAllocations { get; set; }
    /// <summary>
    /// Hạn sử dụng (chỉ dùng khi không có lotAllocations).
    /// Hệ thống sẽ tìm lô khớp ngày hoặc tạo lô mới nếu không tìm thấy.
    /// </summary>
    public DateTime? ExpiredDate { get; set; }
}

/// <summary>Thông tin một lot hàng được trả về kho.</summary>
public class ConfirmReturnLotAllocationDto
{
    public int LotId { get; set; }
    public int QuantityTaken { get; set; }
    /// <summary>Hạn sử dụng của lot (tuỳ chọn, dùng để đối chiếu).</summary>
    public DateTime? ExpiredDate { get; set; }
}

public class ActualReturnedReusableItemDto
{
    public int ItemModelId { get; set; }
    /// <summary>Số lượng (legacy fallback khi không gửi danh sách units).</summary>
    public int? Quantity { get; set; }
    public List<ActualReturnedReusableUnitDto> Units { get; set; } = [];
}

public class ActualReturnedReusableUnitDto
{
    public int ReusableItemId { get; set; }
    /// <summary>Tình trạng thiết bị khi trả về (ví dụ: Good, Damaged, NeedsRepair). Nếu null thì giữ nguyên.</summary>
    public string? Condition { get; set; }
    /// <summary>Ghi chú về tình trạng / sự cố của thiết bị này khi trả về. Nếu null thì giữ nguyên.</summary>
    public string? Note { get; set; }
}
