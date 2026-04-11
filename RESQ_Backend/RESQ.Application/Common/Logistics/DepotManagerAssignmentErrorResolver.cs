using System.Globalization;
using System.Text;
using RESQ.Application.Common.Constants;

namespace RESQ.Application.Common.Logistics;

public static class DepotManagerAssignmentErrorResolver
{
    private static readonly string[] KnownMessageFragments =
    [
        "khong phu trach kho nao",
        "khong quan ly kho nao dang hoat dong",
        "khong duoc chi dinh quan ly bat ky kho nao dang hoat dong",
        "ban hien khong phu trach kho nao",
        "ban khong co kho dang hoat dong",
        "tai khoan hien tai khong duoc chi dinh quan ly bat ky kho nao dang hoat dong",
        "tai khoan khong quan ly kho nao dang hoat dong",
        "tai khoan quan ly kho chua duoc gan kho phu trach"
    ];

    public static string? Resolve(Exception exception)
    {
        var normalizedMessage = Normalize(exception.Message);
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return null;
        }

        foreach (var fragment in KnownMessageFragments)
        {
            if (normalizedMessage.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return LogisticsErrorCodes.DepotManagerNotAssigned;
            }
        }

        return null;
    }

    private static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var decomposed = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(ch switch
            {
                '\u0111' => 'd',
                '\u0110' => 'D',
                _ => ch
            });
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();
    }
}
