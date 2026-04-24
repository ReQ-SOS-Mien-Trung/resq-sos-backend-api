using RESQ.Domain.Entities.Finance.Exceptions;

namespace RESQ.Domain.Entities.Finance;

/// <summary>
/// Quỹ hệ thống - singleton entity chứa tiền thu được từ thanh lý tài sản đóng kho.
/// Admin có thể dùng quỹ này để cấp tiền cho kho (thay vì rút từ chiến dịch).
/// </summary>
public class SystemFundModel
{
    public int Id { get; private set; }
    public string Name { get; private set; } = "Quỹ hệ thống";
    public decimal Balance { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }
    public uint RowVersion { get; private set; }

    private SystemFundModel() { }

    /// <summary>Tạo quỹ hệ thống mới (singleton - chỉ gọi 1 lần khi init DB).</summary>
    public static SystemFundModel Create(string name = "Quỹ hệ thống")
    {
        return new SystemFundModel
        {
            Name = name,
            Balance = 0m,
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Reconstitute từ DB.</summary>
    public static SystemFundModel Reconstitute(int id, string name, decimal balance, DateTime lastUpdatedAt, uint rowVersion = 0)
    {
        return new SystemFundModel
        {
            Id = id,
            Name = name,
            Balance = balance,
            LastUpdatedAt = lastUpdatedAt,
            RowVersion = rowVersion
        };
    }

    /// <summary>
    /// Cộng tiền vào quỹ hệ thống (ví dụ: tiền thanh lý tài sản khi đóng kho).
    /// </summary>
    public void Credit(decimal amount)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);
        Balance += amount;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Trừ tiền từ quỹ hệ thống (ví dụ: cấp tiền cho kho).
    /// Không cho phép balance âm - quỹ hệ thống không có cơ chế tự ứng.
    /// </summary>
    public void Debit(decimal amount)
    {
        if (amount <= 0) throw new NegativeMoneyException(amount);
        if (amount > Balance)
            throw new InsufficientSystemFundException(Balance, amount);
        Balance -= amount;
        LastUpdatedAt = DateTime.UtcNow;
    }
}
