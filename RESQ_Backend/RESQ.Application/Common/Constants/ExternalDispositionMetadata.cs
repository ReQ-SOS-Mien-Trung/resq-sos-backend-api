using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.Common.Constants;

public static class ExternalDispositionMetadata
{
    public static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        [ExternalDispositionType.DonatedToOrganization.ToString()] = "Quyên góp cho t? ch?c / nhân d?o",
        [ExternalDispositionType.Liquidated.ToString()] = "Thanh lý",
        [ExternalDispositionType.Disposed.ToString()] = "Tiêu h?y",
        [ExternalDispositionType.Other.ToString()] = "Khác"
    };

    private static readonly Dictionary<string, ExternalDispositionType> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Donated"] = ExternalDispositionType.DonatedToOrganization,
        ["Sold"] = ExternalDispositionType.Liquidated
    };

    public static string GetLabel(ExternalDispositionType type)
        => Labels.TryGetValue(type.ToString(), out var label) ? label : type.ToString();

    public static string GetDisplayValue(ExternalDispositionType type)
        => $"{type} - {GetLabel(type)}";

    public static ExternalDispositionType? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var normalized = raw.Trim();
        var enumKey = normalized.Split(" - ", 2, StringSplitOptions.None)[0].Trim();

        if (Enum.TryParse<ExternalDispositionType>(enumKey, ignoreCase: true, out var parsed))
            return parsed;

        if (Aliases.TryGetValue(enumKey, out var alias))
            return alias;

        foreach (var value in Enum.GetValues<ExternalDispositionType>())
        {
            if (string.Equals(normalized, GetLabel(value), StringComparison.OrdinalIgnoreCase))
                return value;
        }

        return null;
    }
}
