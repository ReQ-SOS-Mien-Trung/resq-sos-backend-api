namespace RESQ.Domain.Enum.Personnel;

public enum AssemblyPointStatus
{
    /// <summary>Vừa được admin tạo, chưa kích hoạt.</summary>
    Created,

    /// <summary>Đang khả dụng, hoạt động bình thường.</summary>
    Available,

    /// <summary>Không khả dụng (không thể sử dụng).</summary>
    Unavailable,

    /// <summary>Đã đóng vĩnh viễn - không thể chuyển sang bất kỳ trạng thái nào khác.</summary>
    Closed
}
