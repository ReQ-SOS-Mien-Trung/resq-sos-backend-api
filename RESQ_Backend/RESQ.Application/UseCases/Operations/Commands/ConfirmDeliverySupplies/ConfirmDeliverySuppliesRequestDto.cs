using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;

public class ConfirmDeliverySuppliesRequestDto
{
    /// <summary>Danh sach so luong thuc te da giao cho tung loai vat pham trong activity.</summary>
    public List<ActualDeliveredItemDto> ActualDeliveredItems { get; set; } = [];

    /// <summary>Ghi chu khi co chenh lech giua so luong ke hoach va thuc te.</summary>
    public string? DeliveryNote { get; set; }
}

public class ActualDeliveredItemDto
{
    /// <summary>ID cua relief item, khop voi SupplyToCollectDto.ItemId.</summary>
    public int ItemId { get; set; }

    /// <summary>So luong thuc te da giao toi dich. Phai >= 0.</summary>
    public int ActualQuantity { get; set; }

    /// <summary>Consumable lots actually delivered for this item.</summary>
    public List<SupplyExecutionLotDto>? LotAllocations { get; set; }

    /// <summary>Reusable units actually delivered or used at this activity.</summary>
    public List<SupplyExecutionReusableUnitDto>? ReusableUnits { get; set; }
}
