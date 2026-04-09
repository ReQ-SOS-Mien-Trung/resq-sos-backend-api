namespace RESQ.Domain.Enum.Logistics
{
    public enum DepotStatus
    {
        Created,           // Vừa tạo, chưa từng có quản lý
        PendingAssignment, // Đã từng có quản lý, hiện đang chờ gán lại
        Available,
        Unavailable,       // Admin đánh dấu ngưng hoạt động trước khi đóng kho; block mọi thao tác import/export
        Closed
    }
}
