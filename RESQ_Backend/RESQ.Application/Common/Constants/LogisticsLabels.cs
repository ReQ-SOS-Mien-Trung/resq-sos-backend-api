using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Common.Constants;

/// <summary>
/// Nhãn tiếng Việt cho các enum/code logistics dùng cho metadata và Excel template.
/// </summary>
public static class LogisticsLabels
{
    public static readonly Dictionary<string, string> ExternalDispositionTypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        [ExternalDispositionType.DonatedToOrganization.ToString()] = "Quyên góp cho tổ chức / nhân đạo",
        [ExternalDispositionType.Disposed.ToString()] = "Thanh lý / tiêu hủy",
        [ExternalDispositionType.Other.ToString()] = "Khác"
    };

    /// <summary>Tra nhãn tiếng Việt theo key; trả về key gốc nếu không tìm thấy.</summary>
    public static string Translate(Dictionary<string, string> map, string? key)
        => key != null && map.TryGetValue(key, out var label) ? label : (key ?? string.Empty);
}
