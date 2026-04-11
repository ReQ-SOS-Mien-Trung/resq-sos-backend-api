using RESQ.Domain.Entities.Finance.Exceptions;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Domain.Entities.Finance;

/// <summary>
/// Quỹ của kho (depot). Mỗi kho có NHIỀU quỹ — mỗi quỹ gắn với 1 nguồn (chiến dịch hoặc quỹ hệ thống).
/// 
/// === QUY TẮC TÀI CHÍNH MỚI ===
/// 1. Balance KHÔNG BAO GIỜ được âm.
/// 2. Nhập hàng (Debit): nếu quỹ không đủ → từ chối, yêu cầu Ứng trước (Advance).
/// 3. Ứng trước (Advance): cá nhân ứng tiền cho kho → tăng Balance + tăng OutstandingAdvanceAmount.
///    Ràng buộc: OutstandingAdvanceAmount + amount <= AdvanceLimit.
/// 4. Hoàn trả (Repay): kho trả lại tiền ứng → giảm Balance + giảm OutstandingAdvanceAmount.
///    Ràng buộc: amount <= OutstandingAdvanceAmount và amount <= Balance.
/// </summary>
public class DepotFundModel
{
    public int Id { get; private set; }
    public int DepotId { get; private set; }
    public decimal Balance { get; private set; }

    /// <summary>Hạn mức tối đa tổng tiền ứng trước cho kho này. 0 = không cho phép ứng. Admin cấu hình.</summary>
    public decimal AdvanceLimit { get; private set; }

    /// <summary>Tổng số tiền đã ứng trước và chưa hoàn trả.</summary>
    public decimal OutstandingAdvanceAmount { get; private set; }

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
            AdvanceLimit = 0m,
            OutstandingAdvanceAmount = 0m,
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Reconstitute from DB.</summary>
    public static DepotFundModel Reconstitute(
        int id, int depotId, decimal balance, decimal advanceLimit, decimal outstandingAdvanceAmount,
        DateTime lastUpdatedAt, FundSourceType? sourceType = null, int? sourceId = null)
    {
        return new DepotFundModel
        {
            Id = id,
            DepotId = depotId,
            Balance = balance,
            AdvanceLimit = advanceLimit,
            OutstandingAdvanceAmount = outstandingAdvanceAmount,
            LastUpdatedAt = lastUpdatedAt,
            FundSourceType = sourceType,
            FundSourceId = sourceId
        };
    }

    /// <summary>
    /// Cộng quỹ (khi Admin cấp tiền). Đơn giản tăng Balance.
    /// </summary>
    public void Credit(decimal amount)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);

        Balance += amount;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Trừ quỹ (khi Manager nhập hàng). Balance KHÔNG ĐƯỢC ÂM.
    /// Nếu quỹ không đủ → throw InsufficientDepotFundException.
    /// </summary>
    public void Debit(decimal amount)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);

        if (Balance < amount)
            throw new InsufficientDepotFundException(Balance, amount);

        Balance -= amount;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Ứng trước cá nhân cho kho. Tăng Balance (kho có thêm tiền) và tăng OutstandingAdvanceAmount.
    /// Ràng buộc: OutstandingAdvanceAmount + amount <= AdvanceLimit.
    /// </summary>
    public void Advance(decimal amount)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);

        if (OutstandingAdvanceAmount + amount > AdvanceLimit)
            throw new AdvanceLimitExceededException(OutstandingAdvanceAmount, amount, AdvanceLimit);

        Balance += amount;
        OutstandingAdvanceAmount += amount;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Hoàn trả tiền ứng trước cho cá nhân. Giảm Balance và giảm OutstandingAdvanceAmount.
    /// Ràng buộc: amount <= OutstandingAdvanceAmount và amount <= Balance.
    /// </summary>
    public void Repay(decimal amount)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);

        if (amount > OutstandingAdvanceAmount)
            throw new OverRepaymentException(amount, OutstandingAdvanceAmount);

        if (amount > Balance)
            throw new InsufficientDepotFundException(Balance, amount);

        Balance -= amount;
        OutstandingAdvanceAmount -= amount;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Admin cập nhật hạn mức ứng trước tối đa. Không được thấp hơn số dư ứng trước chưa hoàn trả.</summary>
    public void SetAdvanceLimit(decimal limit)
    {
        if (limit < 0) throw new NegativeMoneyException(limit);
        if (limit < OutstandingAdvanceAmount)
            throw new InvalidAdvanceLimitException(limit, OutstandingAdvanceAmount);
        AdvanceLimit = limit;
        LastUpdatedAt = DateTime.UtcNow;
    }
}
