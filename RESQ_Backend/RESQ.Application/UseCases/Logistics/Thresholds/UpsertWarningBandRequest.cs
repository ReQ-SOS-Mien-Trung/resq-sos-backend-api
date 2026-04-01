namespace RESQ.Application.UseCases.Logistics.Thresholds;

/// <summary>
/// Payload cấu hình 4 bậc ngưỡng tồn kho.
/// Chỉ cần nhập giá trị <c>To</c> (%) cho 3 bậc đầu;
/// backend tự tính <c>From</c> dựa trên bậc trước.
/// Bậc OK tự động chiếm toàn bộ phần còn lại (không có giới hạn trên).
/// </summary>
public class UpsertWarningBandRequest
{
    /// <summary>
    /// Giới hạn trên (%) của bậc CRITICAL.
    /// Ví dụ: 40 → tồn kho &lt; 40% ngưỡng tối thiểu = CRITICAL.
    /// </summary>
    public decimal Critical { get; set; }

    /// <summary>
    /// Giới hạn trên (%) của bậc MEDIUM.
    /// Phải lớn hơn <see cref="Critical"/>.
    /// </summary>
    public decimal Medium { get; set; }

    /// <summary>
    /// Giới hạn trên (%) của bậc LOW.
    /// Phải lớn hơn <see cref="Medium"/>.
    /// Bậc OK bắt đầu từ đây trở lên.
    /// </summary>
    public decimal Low { get; set; }
}
