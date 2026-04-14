namespace RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;

public class ConfirmDeliverySuppliesResponse
{
    public int ActivityId { get; set; }
    public int MissionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>ID c?a RETURN_SUPPLIES activity du?c t? d?ng t?o n?u có v?t ph?m giao thi?u, null n?u giao d? ho?c nhi?u hon.</summary>
    public int? SurplusReturnActivityId { get; set; }

    /// <summary>Chi ti?t s? lu?ng k? ho?ch và th?c t? c?a t?ng lo?i v?t ph?m.</summary>
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
