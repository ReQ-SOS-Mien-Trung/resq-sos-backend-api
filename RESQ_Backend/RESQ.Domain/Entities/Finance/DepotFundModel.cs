using RESQ.Domain.Entities.Finance.Exceptions;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance;

/// <summary>Kết quả trừ quỹ — cho biết kho có phải tự ứng hay không.</summary>
public record DebitResult(bool IsAdvanced, decimal AdvancedAmount);

/// <summary>Kết quả cộng quỹ — cho biết đã tự động trừ bao nhiêu nợ.</summary>
public record CreditResult(decimal DebtRepaid, decimal NetCredited);

/// <summary>
/// Quỹ của kho (depot). Mỗi kho có NHIỀU quỹ — mỗi quỹ gắn với 1 nguồn (chiến dịch hoặc quỹ hệ thống).
/// Ví dụ: Kho 1 được cấp từ Chiến dịch A và Chiến dịch B → 2 DepotFund records.
/// Balance tăng khi Admin cấp tiền, giảm khi Manager nhập hàng.
/// Cho phép Balance âm (tự ứng) trong giới hạn MaxAdvanceLimit.
/// </summary>
public class DepotFundModel
{
    public int Id { get; private set; }
    public int DepotId { get; private set; }
    public decimal Balance { get; private set; }

    /// <summary>Hạn mức tối đa quỹ này được phép tự ứng (balance âm). 0 = không cho phép âm. Admin cấu hình.</summary>
    public decimal MaxAdvanceLimit { get; private set; }

    public DateTime LastUpdatedAt { get; private set; }

    // ── Nguồn quỹ (mỗi quỹ kho gắn với 1 nguồn cụ thể) ─────────────

    /// <summary>Loại nguồn quỹ: Campaign hoặc SystemFund.</summary>
    public FundSourceType? FundSourceType { get; private set; }

    /// <summary>
    /// ID nguồn quỹ:
    /// - Nếu FundSourceType = Campaign → FundCampaignId
    /// - Nếu FundSourceType = SystemFund → null (singleton)
    /// - null nếu là quỹ legacy chưa gắn nguồn.
    /// </summary>
    public int? FundSourceId { get; private set; }

    // View properties (populated by mapper/repository)
    public string? DepotName { get; set; }
    public string? FundSourceName { get; set; }

    private DepotFundModel() { }

    /// <summary>Tạo quỹ kho mới với số dư = 0, gắn với nguồn quỹ cụ thể.</summary>
    public static DepotFundModel Create(int depotId, FundSourceType? sourceType = null, int? sourceId = null)
    {
        return new DepotFundModel
        {
            DepotId = depotId,
            FundSourceType = sourceType,
            FundSourceId = sourceId,
            Balance = 0m,
            MaxAdvanceLimit = 0m,
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Reconstitute from DB.</summary>
    public static DepotFundModel Reconstitute(
        int id, int depotId, decimal balance, decimal maxAdvanceLimit, DateTime lastUpdatedAt,
        FundSourceType? sourceType = null, int? sourceId = null)
    {
        return new DepotFundModel
        {
            Id = id,
            DepotId = depotId,
            Balance = balance,
            MaxAdvanceLimit = maxAdvanceLimit,
            LastUpdatedAt = lastUpdatedAt,
            FundSourceType = sourceType,
            FundSourceId = sourceId
        };
    }

    /// <summary>
    /// Cộng quỹ (khi Admin cấp tiền). Nếu balance đang âm (nợ), tự động trừ nợ trước.
    /// Trả về CreditResult chứa thông tin nợ đã trả.
    /// </summary>
    public CreditResult Credit(decimal amount)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);

        decimal debtRepaid = 0m;

        if (Balance < 0)
        {
            debtRepaid = Math.Min(amount, Math.Abs(Balance));
        }

        Balance += amount;
        LastUpdatedAt = DateTime.UtcNow;

        return new CreditResult(debtRepaid, amount - debtRepaid);
    }

    /// <summary>
    /// Trừ quỹ (khi Manager nhập hàng). Cho phép balance âm nếu nằm trong MaxAdvanceLimit.
    /// Nếu vượt quá giới hạn ứng trước → throw AdvanceLimitExceededException.
    /// Trả về DebitResult cho biết kho có tự ứng hay không.
    /// </summary>
    public DebitResult Debit(decimal amount)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);

        var newBalance = Balance - amount;

        if (newBalance < 0 && Math.Abs(newBalance) > MaxAdvanceLimit)
            throw new AdvanceLimitExceededException(Balance, amount, MaxAdvanceLimit);

        // Tính phần tự ứng: phần balance đi vào vùng âm
        var advancedAmount = 0m;
        if (newBalance < 0)
        {
            advancedAmount = Balance >= 0 ? Math.Abs(newBalance) : amount;
        }

        Balance = newBalance;
        LastUpdatedAt = DateTime.UtcNow;

        return new DebitResult(
            IsAdvanced: advancedAmount > 0,
            AdvancedAmount: advancedAmount
        );
    }

    /// <summary>Admin cập nhật hạn mức tự ứng tối đa.</summary>
    public void SetMaxAdvanceLimit(decimal limit)
    {
        if (limit < 0) throw new NegativeMoneyException(limit);
        MaxAdvanceLimit = limit;
        LastUpdatedAt = DateTime.UtcNow;
    }
}
