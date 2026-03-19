using RESQ.Domain.Entities.Finance.Exceptions;

namespace RESQ.Domain.Entities.Finance;

/// <summary>
/// Quỹ của mỗi kho (depot). Mỗi kho có đúng 1 record.
/// Balance tăng khi Admin cấp tiền, giảm khi Manager nhập hàng.
/// </summary>
public class DepotFundModel
{
    public int Id { get; private set; }
    public int DepotId { get; private set; }
    public decimal Balance { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }

    // View properties (populated by mapper/repository)
    public string? DepotName { get; set; }

    private DepotFundModel() { }

    /// <summary>Tạo quỹ kho mới với số dư = 0.</summary>
    public static DepotFundModel Create(int depotId)
    {
        return new DepotFundModel
        {
            DepotId = depotId,
            Balance = 0m,
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Reconstitute from DB.</summary>
    public static DepotFundModel Reconstitute(int id, int depotId, decimal balance, DateTime lastUpdatedAt)
    {
        return new DepotFundModel
        {
            Id = id,
            DepotId = depotId,
            Balance = balance,
            LastUpdatedAt = lastUpdatedAt
        };
    }

    /// <summary>Cộng quỹ (khi Admin cấp tiền).</summary>
    public void Credit(decimal amount)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);
        Balance += amount;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Trừ quỹ (khi Manager nhập hàng).</summary>
    public void Debit(decimal amount)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);
        if (Balance < amount)
            throw new InsufficientDepotFundException(Balance, amount);
        Balance -= amount;
        LastUpdatedAt = DateTime.UtcNow;
    }
}
