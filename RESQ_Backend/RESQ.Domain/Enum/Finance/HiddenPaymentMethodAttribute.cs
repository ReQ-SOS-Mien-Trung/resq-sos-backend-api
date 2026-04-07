namespace RESQ.Domain.Enum.Finance;

/// <summary>
/// Đánh dấu một giá trị <see cref="PaymentMethodCode"/> là ẩn khỏi API công khai
/// (ví dụ: metadata endpoint). Giá trị vẫn hợp lệ khi parse dữ liệu cũ trong DB,
/// nhưng không thể được chọn cho đơn mới.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class HiddenPaymentMethodAttribute : Attribute { }
