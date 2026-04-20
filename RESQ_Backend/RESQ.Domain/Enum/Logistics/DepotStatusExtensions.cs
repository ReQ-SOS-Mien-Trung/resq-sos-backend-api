namespace RESQ.Domain.Enum.Logistics;

public static class DepotStatusExtensions
{
    public static string ToVietnamese(this DepotStatus status) => status switch
    {
        DepotStatus.Created => "Mới tạo",
        DepotStatus.PendingAssignment => "Chờ phân công quản lý",
        DepotStatus.Available => "Đang hoạt động",
        DepotStatus.Unavailable => "Ngưng hoạt động",
        DepotStatus.Closing => "Đang đóng kho",
        DepotStatus.Closed => "Đã đóng vĩnh viễn",
        _ => status.ToString()
    };
}
