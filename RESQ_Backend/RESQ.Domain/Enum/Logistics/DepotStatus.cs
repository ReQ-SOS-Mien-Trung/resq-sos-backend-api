namespace RESQ.Domain.Enum.Logistics
{
    public enum DepotStatus
    {
        PendingAssignment,
        Available,
        Full,
        UnderMaintenance, // Tạm ngưng hoạt động để bảo trì; có thể trở lại Available
        Closing,          // Trạng thái trung gian khi admin đang thực hiện đóng kho
        Closed
    }
}
