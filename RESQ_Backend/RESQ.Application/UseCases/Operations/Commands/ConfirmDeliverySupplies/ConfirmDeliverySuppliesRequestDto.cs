namespace RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;

public class ConfirmDeliverySuppliesRequestDto
{
    /// <summary>Danh sách số lượng thực tế đã giao cho từng loại vật tư trong activity.</summary>
    public List<ActualDeliveredItemDto> ActualDeliveredItems { get; set; } = [];

    /// <summary>Ghi chú khi có chênh lệch giữa số lượng kế hoạch và thực tế.</summary>
    public string? DeliveryNote { get; set; }
}

public class ActualDeliveredItemDto
{
    /// <summary>ID của relief item (khớp với SupplyToCollectDto.ItemId).</summary>
    public int ItemId { get; set; }

    /// <summary>Số lượng thực tế đã giao tới đích. Phải >= 0.</summary>
    public int ActualQuantity { get; set; }
}
