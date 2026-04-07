using System.ComponentModel;
using System.Reflection;

namespace RESQ.Domain.Enum.Finance;

public static class PaymentMethodCodeExtensions
{
    /// <summary>
    /// Trả về true nếu phương thức thanh toán bị ẩn khỏi API công khai
    /// (được đánh dấu bằng <see cref="HiddenPaymentMethodAttribute"/>).
    /// </summary>
    public static bool IsHidden(this PaymentMethodCode code)
    {
        return typeof(PaymentMethodCode)
            .GetField(code.ToString())
            ?.GetCustomAttribute<HiddenPaymentMethodAttribute>() is not null;
    }

    /// <summary>
    /// Trả về display name từ <see cref="DescriptionAttribute"/>,
    /// hoặc tên enum nếu không có attribute.
    /// </summary>
    public static string GetDescription(this PaymentMethodCode code)
    {
        return typeof(PaymentMethodCode)
            .GetField(code.ToString())
            ?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? code.ToString();
    }
}
