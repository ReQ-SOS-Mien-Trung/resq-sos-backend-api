using System.Globalization;
using System.Text;
using RESQ.Application.Common.Constants;

namespace RESQ.Application.Common.Logistics;

public static class DepotManagerAssignmentErrorResolver
{
    private static readonly string[] KnownMessageFragments =
    [
        "không ph? trách kho nào",
        "không qu?n lý kho nào dang ho?t d?ng",
        "không du?c ch? d?nh qu?n lý b?t k? kho nào dang ho?t d?ng",
        "b?n hi?n không ph? trách kho nào",
        "b?n không có kho dang ho?t d?ng",
        "tài kho?n hi?n t?i không du?c ch? d?nh qu?n lý b?t k? kho nào dang ho?t d?ng",
        "tài kho?n không qu?n lý kho nào dang ho?t d?ng",
        "tài kho?n qu?n lý kho chua du?c gán kho ph? trách"
    ];

    public static string? Resolve(Exception exception)
    {
        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        foreach (var fragment in KnownMessageFragments)
        {
            if (message.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return LogisticsErrorCodes.DepotManagerNotAssigned;
            }
        }

        return null;
    }
}

