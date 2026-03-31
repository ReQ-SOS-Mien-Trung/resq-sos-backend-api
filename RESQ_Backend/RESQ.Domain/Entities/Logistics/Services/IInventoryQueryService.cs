using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Domain.Entities.Logistics.Services;

public interface IInventoryQueryService
{
    /// <summary>
    /// Tính toán InventoryAvailability từ các giá trị raw DB.
    /// </summary>
    /// <param name="quantity">Raw DB: tổng tồn kho.</param>
    /// <param name="missionReserved">Raw DB: mission_reserved_quantity.</param>
    /// <param name="transferReserved">Raw DB: transfer_reserved_quantity.</param>
    InventoryAvailability ComputeAvailability(int? quantity, int? missionReserved, int? transferReserved);
}
