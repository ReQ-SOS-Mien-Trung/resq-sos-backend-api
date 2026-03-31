using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.ValueObjects;

public sealed class InvalidWarningBandSetException(string message) : DomainException(message);

/// <summary>
/// Validated stock warning bands.
/// The system supports exactly four fixed levels: CRITICAL, MEDIUM, LOW, OK.
/// </summary>
public sealed class WarningBandSet
{
    private static readonly (string Name, decimal From, decimal? To)[] FixedBands =
    [
        ("CRITICAL", 0.0m, 0.4m),
        ("MEDIUM",   0.4m, 0.7m),
        ("LOW",      0.7m, 1.0m),
        ("OK",       1.0m, null)
    ];

    public IReadOnlyList<WarningBand> Bands { get; }

    public static string? ValidateFixedBandDefinition(IReadOnlyList<WarningBand>? bands)
    {
        if (bands == null || bands.Count != FixedBands.Length)
            return "Warning bands phai gom dung 4 muc: CRITICAL, MEDIUM, LOW, OK.";

        var sorted = bands.OrderBy(b => b.From).ToList();

        for (var i = 0; i < FixedBands.Length; i++)
        {
            var actual = sorted[i];
            var expected = FixedBands[i];

            if (!string.Equals(actual.Name, expected.Name, StringComparison.Ordinal))
                return $"Band tai vi tri {i + 1} phai la '{expected.Name}'.";

            if (actual.From != expected.From || actual.To != expected.To)
            {
                var expectedTo = expected.To?.ToString("0.##") ?? "null";
                var actualTo = actual.To?.ToString("0.##") ?? "null";
                return
                    $"Band '{expected.Name}' phai co range [{expected.From:0.##}, {expectedTo}), " +
                    $"nhung hien tai la [{actual.From:0.##}, {actualTo}).";
            }
        }

        return null;
    }

    public WarningBandSet(IReadOnlyList<WarningBand> bands)
    {
        if (bands == null || bands.Count == 0)
            throw new InvalidWarningBandSetException("Danh sach warning bands khong duoc rong.");

        foreach (var band in bands)
        {
            if (string.IsNullOrWhiteSpace(band.Name))
                throw new InvalidWarningBandSetException("Moi band phai co ten.");

            if (band.From < 0)
                throw new InvalidWarningBandSetException($"Band '{band.Name}': From phai >= 0.");

            if (band.To.HasValue && band.To.Value <= band.From)
                throw new InvalidWarningBandSetException($"Band '{band.Name}': To phai > From.");
        }

        var sorted = bands.OrderBy(b => b.From).ToList();

        if (sorted[0].From != 0m)
            throw new InvalidWarningBandSetException("Band dau tien phai bat dau tu 0.");

        if (sorted[^1].To.HasValue)
            throw new InvalidWarningBandSetException("Band cuoi cung phai co To = null.");

        for (var i = 0; i < sorted.Count - 1; i++)
        {
            var current = sorted[i];
            var next = sorted[i + 1];

            if (!current.To.HasValue)
                throw new InvalidWarningBandSetException(
                    $"Band '{current.Name}': chi band cuoi cung moi duoc co To = null.");

            if (current.To.Value != next.From)
                throw new InvalidWarningBandSetException(
                    $"Gap hoac overlap giua band '{current.Name}' (to={current.To}) va '{next.Name}' (from={next.From}).");
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
