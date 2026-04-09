namespace RESQ.Domain.Enum.Finance;

/// <summary>
/// Loại nguồn quỹ: Chiến dịch quyên góp hoặc Quỹ hệ thống (tiền thanh lý).
/// </summary>
public enum FundSourceType
{
    /// <summary>Quỹ đến từ một chiến dịch quyên góp cụ thể.</summary>
    Campaign,

    /// <summary>Quỹ đến từ quỹ hệ thống (tiền thanh lý tài sản khi đóng kho, v.v.).</summary>
    SystemFund
}
