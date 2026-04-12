using System.Globalization;
using System.Text;
using RESQ.Application.Common.Constants;

namespace RESQ.Application.Common.Logistics;

public static class DepotManagerAssignmentErrorResolver
{
    private static readonly string[] KnownMessageFragments =
    [
        "không phụ trách kho nào",
        "không quản lý kho nào đang hoạt động",
        "không được chỉ định quản lý bất kỳ kho nào đang hoạt động",
        "bạn hiện không phụ trách kho nào",
        "bạn không có kho đang hoạt động",
        "tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động",
        "tài khoản không quản lý kho nào đang hoạt động",
        "tài khoản quản lý kho chưa được gán kho phụ trách"
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

