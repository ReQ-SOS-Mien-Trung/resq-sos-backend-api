namespace RESQ.Domain.Enum.Personnel;

public enum AssemblyPointStatus
{
    /// <summary>Vừa được admin tạo, chưa kích hoạt.</summary>
    Created,

    /// <summary>Đang hoạt động bình thường.</summary>
    Active,

    /// <summary>Đã đầy — không thể gán thêm đội.</summary>
    Overloaded,

    /// <summary>Đang bảo trì (vào từ Active hoặc Overloaded).</summary>
    UnderMaintenance,

    /// <summary>Đã đóng vĩnh viễn — không thể chuyển sang bất kỳ trạng thái nào khác.</summary>
    Closed
}
