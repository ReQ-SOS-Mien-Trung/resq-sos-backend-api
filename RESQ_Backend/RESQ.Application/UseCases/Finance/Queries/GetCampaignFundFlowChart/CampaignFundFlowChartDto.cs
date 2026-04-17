namespace RESQ.Application.UseCases.Finance.Queries.GetCampaignFundFlowChart;

/// <summary>
/// Một cột dữ liệu trong biểu đồ biến động quỹ chiến dịch (bar chart – 3 cột mỗi kỳ).
/// </summary>
public class CampaignFundFlowDataPoint
{
    /// <summary>Nhãn kỳ (vd: "2024-10" cho tháng, hoặc "2024-W42" cho tuần).</summary>
    public string PeriodLabel { get; set; } = string.Empty;

    /// <summary>Tổng tiền vào (quyên góp, nạp quỹ…) trong kỳ.</summary>
    public decimal TotalIn { get; set; }

    /// <summary>Tổng tiền ra (cấp phát cho kho, chi tiêu…) trong kỳ.</summary>
    public decimal TotalOut { get; set; }

    /// <summary>Số dư thuần trong kỳ (TotalIn - TotalOut).</summary>
    public decimal NetBalance { get; set; }
}

/// <summary>
/// Response biểu đồ biến động quỹ chiến dịch – bar chart 3 cột (In / Out / Net).
/// </summary>
public class CampaignFundFlowChartDto
{
    public int CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;

    /// <summary>Đơn vị nhóm: "month" hoặc "week".</summary>
    public string Granularity { get; set; } = "month";

    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<CampaignFundFlowDataPoint> DataPoints { get; set; } = [];
}
