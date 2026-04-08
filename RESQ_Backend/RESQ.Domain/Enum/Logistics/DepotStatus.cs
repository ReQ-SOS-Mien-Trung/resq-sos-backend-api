namespace RESQ.Domain.Enum.Logistics
{
    public enum DepotStatus
    {
        Created,           // Vừa tạo, chưa từng có quản lý
        PendingAssignment, // Đã từng có quản lý, hiện đang chờ gán lại
        Available,
        Full,
        UnderMaintenance, // Tạm ngưng hoạt động để bảo trì; có thể trở lại Available
        Closing,          // Trạng thái trung gian khi admin đang thực hiện đóng kho
        Closed
    }
}
