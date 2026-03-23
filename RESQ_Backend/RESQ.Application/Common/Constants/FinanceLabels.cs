using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.Common.Constants;

/// <summary>
/// Nhãn tiếng Việt cho các enum / code tài chính — dùng cho metadata endpoints và response labels.
/// </summary>
public static class FinanceLabels
{
    public static readonly Dictionary<string, string> FundCampaignStatusLabels = new()
    {
        [FundCampaignStatus.Draft.ToString()]     = "Bản nháp",
        [FundCampaignStatus.Active.ToString()]    = "Đang hoạt động",
        [FundCampaignStatus.Suspended.ToString()] = "Tạm dừng",
        [FundCampaignStatus.Closed.ToString()]    = "Đã đóng",
        [FundCampaignStatus.Archived.ToString()]  = "Đã lưu trữ"
    };

    public static readonly Dictionary<string, string> TransactionTypeLabels = new()
    {
        [TransactionType.Donation.ToString()]   = "Tiếp nhận quyên góp",
        [TransactionType.Allocation.ToString()] = "Cấp phát",
        [TransactionType.Adjustment.ToString()] = "Điều chỉnh"
    };

    public static readonly Dictionary<string, string> TransactionReferenceTypeLabels = new()
    {
        [TransactionReferenceType.Donation.ToString()]              = "Quyên góp",
        [TransactionReferenceType.CampaignDisbursement.ToString()]  = "Cấp phát cho kho",
        [TransactionReferenceType.FundingRequest.ToString()]        = "Yêu cầu cấp vốn"
    };

    public static readonly Dictionary<string, string> DirectionLabels = new()
    {
        [TransactionDirection.In.ToString()]  = "Tiền vào",
        [TransactionDirection.Out.ToString()] = "Tiền ra"
    };

    public static readonly Dictionary<string, string> DepotFundTransactionTypeLabels = new()
    {
        [DepotFundTransactionType.Allocation.ToString()]    = "Cấp quỹ",
        [DepotFundTransactionType.Deduction.ToString()]     = "Thanh toán mua hàng",
        [DepotFundTransactionType.Refund.ToString()]        = "Hoàn quỹ",
        [DepotFundTransactionType.SelfAdvance.ToString()]   = "Tự ứng",
        [DepotFundTransactionType.DebtRepayment.ToString()] = "Trả nợ tự ứng"
    };

    public static readonly Dictionary<string, string> DepotFundReferenceTypeLabels = new()
    {
        ["CampaignDisbursement"] = "Cấp phát từ chiến dịch",
        ["VatInvoice"]           = "Hóa đơn VAT"
    };

    /// <summary>Tra nhãn tiếng Việt theo key; trả về key gốc nếu không tìm thấy.</summary>
    public static string Translate(Dictionary<string, string> map, string? key)
        => key != null && map.TryGetValue(key, out var label) ? label : (key ?? string.Empty);
}
