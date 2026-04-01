namespace RESQ.Application.UseCases.Logistics.Thresholds;

/// <summary>
/// Phản hồi cấu hình 4 bậc ngưỡng tồn kho.
/// Mỗi giá trị là giới hạn trên (%) của bậc tương ứng;
/// bậc OK không có giới hạn trên.
/// </summary>
public class WarningBandConfigResponse
{
    public int Id { get; set; }

    /// <summary>Giới hạn trên (%) của bậc CRITICAL. Ví dụ: 40.</summary>
    public decimal Critical { get; set; }

    /// <summary>Giới hạn trên (%) của bậc MEDIUM. Ví dụ: 70.</summary>
    public decimal Medium { get; set; }

    /// <summary>Giới hạn trên (%) của bậc LOW. Bậc OK bắt đầu từ đây trở lên.</summary>
    public decimal Low { get; set; }

    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
