using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.ValueObjects;

public sealed class InvalidWarningBandSetException(string message) : DomainException(message);

/// <summary>
/// Tập hợp các dải cảnh báo tồn kho đã được validate.
/// Constructor đảm bảo: sorted, không gap, không overlap, cover từ 0 đến +∞.
/// </summary>
public sealed class WarningBandSet
{
    public IReadOnlyList<WarningBand> Bands { get; }

    public WarningBandSet(IReadOnlyList<WarningBand> bands)
    {
        if (bands == null || bands.Count == 0)
            throw new InvalidWarningBandSetException("Danh sách warning bands không được rỗng.");

        // Validate từng band
        foreach (var band in bands)
        {
            if (string.IsNullOrWhiteSpace(band.Name))
                throw new InvalidWarningBandSetException("Mỗi band phải có tên.");

            if (band.From < 0)
                throw new InvalidWarningBandSetException($"Band '{band.Name}': From phải >= 0.");

            if (band.To.HasValue && band.To.Value <= band.From)
                throw new InvalidWarningBandSetException($"Band '{band.Name}': To phải > From.");
        }

        // Sắp xếp theo From tăng dần
        var sorted = bands.OrderBy(b => b.From).ToList();

        // Band đầu phải bắt đầu từ 0
        if (sorted[0].From != 0m)
            throw new InvalidWarningBandSetException("Band đầu tiên phải bắt đầu từ 0.");

        // Band cuối phải có To = null (không giới hạn trên)
        if (sorted[^1].To.HasValue)
            throw new InvalidWarningBandSetException("Band cuối cùng phải có To = null (không giới hạn trên).");

        // Kiểm tra không gap và không overlap: mỗi band[i].To phải == band[i+1].From
        for (var i = 0; i < sorted.Count - 1; i++)
        {
            var current = sorted[i];
            var next = sorted[i + 1];

            if (!current.To.HasValue)
                throw new InvalidWarningBandSetException(
                    $"Band '{current.Name}': chỉ band cuối cùng mới được có To = null.");

            if (current.To.Value != next.From)
                throw new InvalidWarningBandSetException(
                    $"Gap hoặc overlap giữa band '{current.Name}' (to={current.To}) và '{next.Name}' (from={next.From}).");
        }

        Bands = sorted;
    }

    /// <summary>
    /// Tìm tên level tương ứng với ratio.
    /// Rule: From &lt;= ratio &lt; To (null To = không giới hạn trên).
    /// </summary>
    public string Match(decimal ratio)
    {
        foreach (var band in Bands)
        {
            if (ratio >= band.From && (!band.To.HasValue || ratio < band.To.Value))
                return band.Name;
        }

        // Không bao giờ xảy ra nếu bands cover đúng từ 0 đến +∞
        return Bands[^1].Name;
    }
}
