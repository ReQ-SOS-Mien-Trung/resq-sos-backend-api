namespace RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;

public class ConfirmDeliverySuppliesRequestDto
{
    /// <summary>Danh sách s? lu?ng th?c t? dã giao cho t?ng lo?i v?t ph?m trong activity.</summary>
    public List<ActualDeliveredItemDto> ActualDeliveredItems { get; set; } = [];

    /// <summary>Ghi chú khi có chênh l?ch gi?a s? lu?ng k? ho?ch và th?c t?.</summary>
    public string? DeliveryNote { get; set; }
}

public class ActualDeliveredItemDto
{
    /// <summary>ID c?a relief item (kh?p v?i SupplyToCollectDto.ItemId).</summary>
    public int ItemId { get; set; }

    /// <summary>S? lu?ng th?c t? dã giao t?i dích. Ph?i >= 0.</summary>
    public int ActualQuantity { get; set; }
}
