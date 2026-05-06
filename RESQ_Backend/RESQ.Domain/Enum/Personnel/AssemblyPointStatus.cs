namespace RESQ.Domain.Enum.Personnel;

public enum AssemblyPointStatus
{
    /// <summary>Vừa được tạo, chưa kích hoạt.</summary>
    Created,

    /// <summary>Đang khả dụng, hoạt động bình thường.</summary>
    Available,

    /// <summary>Đang chờ điều phối lại tài nguyên bị ảnh hưởng trước khi chuyển sang Không khả dụng.</summary>
    PendingUnavailable,

    /// <summary>Không khả dụng cho hoạt động.</summary>
    Unavailable,

    /// <summary>Đã đóng vĩnh viễn — không thể chuyển sang bất kỳ trạng thái nào khác.</summary>
    Closed
}
