namespace RESQ.Domain.Enum.Logistics;

public enum ReusableItemStatus
{
    Available,
    Reserved,       // Đã đặt trữ cho một yêu cầu tiếp tế (chờ xuất kho)
    InTransit,      // Đang vận chuyển đến kho đích
    InUse,
    Maintenance,
    Decommissioned
}
