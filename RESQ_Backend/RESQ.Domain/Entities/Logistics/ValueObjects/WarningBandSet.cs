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
            return $"Warning bands ph?i g?m dúng {FixedBandNames.Length} m?c: {string.Join(", ", FixedBandNames)}.";

        var sorted = bands.OrderBy(b => b.From).ToList();

        for (var i = 0; i < FixedBandNames.Length; i++)
        {
            var actual   = sorted[i];
            var expected = FixedBandNames[i];

            if (!string.Equals(actual.Name, expected, StringComparison.Ordinal))
                return $"Band t?i v? trí {i + 1} ph?i là '{expected}'.";
        }

        return null;
    }

    public WarningBandSet(IReadOnlyList<WarningBand> bands)
    {
        if (bands == null || bands.Count == 0)
            throw new InvalidWarningBandSetException("Danh sách warning bands không du?c r?ng.");

        foreach (var band in bands)
        {
            if (string.IsNullOrWhiteSpace(band.Name))
                throw new InvalidWarningBandSetException("M?i band ph?i có tên.");

            if (band.From < 0)
                throw new InvalidWarningBandSetException($"Band '{band.Name}': From ph?i >= 0.");

            if (band.To.HasValue && band.To.Value <= band.From)
                throw new InvalidWarningBandSetException($"Band '{band.Name}': To ph?i > From.");
        }

        var sorted = bands.OrderBy(b => b.From).ToList();

        if (sorted[0].From != 0m)
            throw new InvalidWarningBandSetException("Band d?u tiên ph?i b?t d?u t? 0.");

        if (sorted[^1].To.HasValue)
            throw new InvalidWarningBandSetException("Band cu?i cùng ph?i có To = null.");

        for (var i = 0; i < sorted.Count - 1; i++)
        {
            var current = sorted[i];
            var next = sorted[i + 1];

            if (!current.To.HasValue)
                throw new InvalidWarningBandSetException(
                    $"Band '{current.Name}': ch? band cu?i cùng m?i du?c có To = null.");

            if (current.To.Value != next.From)
                throw new InvalidWarningBandSetException(
                    $"Gap ho?c overlap gi?a band '{current.Name}' (to={current.To}) và '{next.Name}' (from={next.From}).");
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
