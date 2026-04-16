using System;
using System.Globalization;
using System.Text.RegularExpressions;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.Common;

public static class PromptLifecycleStatusResolver
{
    private const string DraftMarker = "-D";
    private const int MaxVersionLength = 20;
    private static readonly Regex DraftVersionRegex = new(
        "^(?<root>.+?)-D(?<stamp>\\d{8})(?<suffix>\\d*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsDraft(PromptModel prompt)
    {
        return !prompt.IsActive && IsDraftVersion(prompt.Version);
    }

    public static bool IsDraftVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        return DraftVersionRegex.IsMatch(version.Trim());
    }

    public static string DetermineStatus(PromptModel prompt)
    {
        if (prompt.IsActive)
        {
            return "Active";
        }

        return IsDraft(prompt) ? "Draft" : "Archived";
    }

    public static string ResolveVersionRoot(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "1.0";
        }

        var normalized = version.Trim();
        var match = DraftVersionRegex.Match(normalized);
        if (match.Success)
        {
            return match.Groups["root"].Value;
        }

        return normalized;
    }

    public static string NormalizeReleasedVersion(string? version)
    {
        var versionRoot = ResolveVersionRoot(version);
        return string.IsNullOrWhiteSpace(versionRoot) ? "1.0" : versionRoot.Trim();
    }

    public static string BuildDraftVersionCandidate(string versionRoot, DateTime utcNow, int? suffix = null)
    {
        var normalizedRoot = NormalizeReleasedVersion(versionRoot);
        var timestamp = utcNow.ToString("yyMMddHH", CultureInfo.InvariantCulture);
        var suffixText = suffix.HasValue ? suffix.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;

        var reservedLength = DraftMarker.Length + timestamp.Length + suffixText.Length;
        var maxRootLength = Math.Max(1, MaxVersionLength - reservedLength);
        if (normalizedRoot.Length > maxRootLength)
        {
            normalizedRoot = normalizedRoot[..maxRootLength];
        }

        return $"{normalizedRoot}{DraftMarker}{timestamp}{suffixText}";
    }
}
