namespace RESQ.Domain.Enum.Logistics
{
    public enum DepotStatus
    {
        Created = 0,           // Vừa tạo, chưa từng có quản lý
        PendingAssignment = 1, // Đã từng có quản lý, hiện đang chờ gán lại
        Available = 2,
        Unavailable = 3,       // Tạm ngưng hoạt động, không cho xuất nhập
        Closed = 4,            // Đã đóng vĩnh viễn
        Closing = 5            // Đang trong quá trình đóng kho (chờ giải quyết tồn kho)
    }
}
