using RESQ.Domain.Entities.Logistics.Exceptions;

namespace RESQ.Domain.Entities.Logistics.ValueObjects;

public record InventoryAvailability
{
    public int Quantity { get; }

    /// <summary>Số lượng đặt trước thuần cho nhiệm vụ.</summary>
    public int MissionReservedQuantity { get; }

    /// <summary>Số lượng đặt trước thuần cho tiếp tế giữa kho.</summary>
    public int TransferReservedQuantity { get; }

    /// <summary>Tổng = MissionReservedQuantity + TransferReservedQuantity. Chỉ được tính, không stored.</summary>
    public int TotalReservedQuantity => MissionReservedQuantity + TransferReservedQuantity;

    public int AvailableQuantity { get; }

    /// <param name="quantity">Raw DB value: tổng tồn kho.</param>
    /// <param name="missionReserved">Raw DB value: mission_reserved_quantity.</param>
    /// <param name="transferReserved">Raw DB value: transfer_reserved_quantity.</param>
    public InventoryAvailability(int quantity, int missionReserved, int transferReserved)
    {
        if (quantity < 0)
            throw new InvalidInventoryQuantityException();

        if (missionReserved < 0)
            throw new InvalidReservedQuantityException();

        if (transferReserved < 0)
            throw new InvalidReservedQuantityException();

        if (missionReserved + transferReserved > quantity)
            throw new ReservedQuantityExceedsTotalException();

        Quantity                 = quantity;
        MissionReservedQuantity  = missionReserved;
        TransferReservedQuantity = transferReserved;
        AvailableQuantity        = quantity - (missionReserved + transferReserved);
    }
}
