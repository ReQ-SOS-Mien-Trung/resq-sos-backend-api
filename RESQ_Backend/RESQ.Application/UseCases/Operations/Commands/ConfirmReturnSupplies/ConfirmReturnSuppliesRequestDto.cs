using RESQ.Application.Common.Models;

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
    public int Quantity { get; set; }
    public List<SupplyExecutionLotDto>? LotAllocations { get; set; }
    /// <summary>
    /// Hạn sử dụng in trên bao bì của sản phẩm trả về.
    /// Nếu cung cấp, hệ thống sẽ tìm lô có expired_date khớp và cộng số lượng vào đó.
    /// Nếu null hoặc không tìm thấy lô phù hợp, hệ thống sẽ tạo lô mới.
    /// </summary>
    public DateTime? ExpiredDate { get; set; }
}

public class ActualReturnedReusableItemDto
{
    public int ItemModelId { get; set; }
    public int? Quantity { get; set; }
    public List<ActualReturnedReusableUnitDto> Units { get; set; } = [];
}

public class ActualReturnedReusableUnitDto
{
    public int ReusableItemId { get; set; }
    public string? SerialNumber { get; set; }
    /// <summary>Tình trạng thiết bị khi trả về (ví dụ: Good, Damaged, NeedsRepair). Nếu null thì giữ nguyên.</summary>
    public string? Condition { get; set; }
    /// <summary>Ghi chú về tình trạng / sự cố của thiết bị này khi trả về. Nếu null thì giữ nguyên.</summary>
    public string? Note { get; set; }
}
