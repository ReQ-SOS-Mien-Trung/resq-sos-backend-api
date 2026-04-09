namespace RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;

public class ConfirmDeliverySuppliesResponse
{
    public int ActivityId { get; set; }
    public int MissionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>ID của RETURN_SUPPLIES activity được tự động tạo nếu có vật tư giao thiếu, null nếu giao đủ hoặc nhiều hơn.</summary>
    public int? SurplusReturnActivityId { get; set; }

    /// <summary>Chi tiết số lượng kế hoạch và thực tế của từng loại vật tư.</summary>
    public List<DeliveryItemResultDto> DeliveredItems { get; set; } = [];
}

public class DeliveryItemResultDto
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int PlannedQuantity { get; set; }
    public int ActualDeliveredQuantity { get; set; }
    public int SurplusQuantity { get; set; }
}
