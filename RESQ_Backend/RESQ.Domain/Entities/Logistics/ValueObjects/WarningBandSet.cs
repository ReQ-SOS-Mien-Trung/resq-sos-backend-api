using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.ValueObjects;

public sealed class InvalidWarningBandSetException(string message) : DomainException(message);

/// <summary>
/// Validated stock warning bands.
/// The system supports exactly four fixed levels: CRITICAL, MEDIUM, LOW, OK.
/// From values are derived automatically from adjacent bands (no need to provide in request).
/// </summary>
public sealed class WarningBandSet
{
    /// <summary>The four required band names in ascending order of stock level.</summary>
    public static readonly string[] FixedBandNames = ["CRITICAL", "MEDIUM", "LOW", "OK"];

    public IReadOnlyList<WarningBand> Bands { get; }

    public static string? ValidateFixedBandDefinition(IReadOnlyList<WarningBand>? bands)
    {
        if (bands == null || bands.Count != FixedBandNames.Length)
            return $"Warning bands phải gồm đúng {FixedBandNames.Length} mức: {string.Join(", ", FixedBandNames)}.";

        var sorted = bands.OrderBy(b => b.From).ToList();

        for (var i = 0; i < FixedBandNames.Length; i++)
        {
            var actual   = sorted[i];
            var expected = FixedBandNames[i];

            if (!string.Equals(actual.Name, expected, StringComparison.Ordinal))
                return $"Band tại vị trí {i + 1} phải là '{expected}'.";
        }

        return null;
    }

    public WarningBandSet(IReadOnlyList<WarningBand> bands)
    {
        if (bands == null || bands.Count == 0)
            throw new InvalidWarningBandSetException("Danh sách warning bands không được rỗng.");

        foreach (var band in bands)
        {
            if (string.IsNullOrWhiteSpace(band.Name))
                throw new InvalidWarningBandSetException("Mỗi band phải có tên.");

            if (band.From < 0)
                throw new InvalidWarningBandSetException($"Band '{band.Name}': From phải >= 0.");

            if (band.To.HasValue && band.To.Value <= band.From)
                throw new InvalidWarningBandSetException($"Band '{band.Name}': To phải > From.");
        }

        var sorted = bands.OrderBy(b => b.From).ToList();

        if (sorted[0].From != 0m)
            throw new InvalidWarningBandSetException("Band đầu tiên phải bắt đầu từ 0.");

        if (sorted[^1].To.HasValue)
            throw new InvalidWarningBandSetException("Band cuối cùng phải có To = null.");

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

        var fixedBandValidationError = ValidateFixedBandDefinition(sorted);
        if (fixedBandValidationError != null)
            throw new InvalidWarningBandSetException(fixedBandValidationError);

        Bands = sorted;
    }

    public string Match(decimal ratio)
    {
        foreach (var band in Bands)
        {
            if (ratio >= band.From && (!band.To.HasValue || ratio < band.To.Value))
                return band.Name;
        }

        return Bands[^1].Name;
    }
}
