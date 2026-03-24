namespace RESQ.Application.Common.Constants;

/// <summary>
/// Utility để dịch tên nhóm đối tượng từ tiếng Anh (lưu trong DB) sang tiếng Việt (hiển thị cho người dùng).
/// </summary>
public static class TargetGroupTranslations
{
    /// <summary>
    /// Dịch một tên nhóm đối tượng đơn lẻ sang tiếng Việt.
    /// Trả về tên gốc nếu không tìm thấy bản dịch.
    /// </summary>
    public static string ToVietnamese(string englishName) => englishName.Trim() switch
    {
        "Children" => "Trẻ em",
        "Elderly"  => "Người già",
        "Pregnant" => "Phụ nữ mang thai",
        "Adult"    => "Người lớn",
        "Rescuer"  => "Lực lượng cứu hộ",
        var other  => other
    };

    /// <summary>
    /// Dịch danh sách tên nhóm đối tượng và nối lại thành chuỗi phân cách bởi dấu phẩy.
    /// </summary>
    public static string JoinAsVietnamese(IEnumerable<string> englishNames)
        => string.Join(", ", englishNames.Select(ToVietnamese));
}
