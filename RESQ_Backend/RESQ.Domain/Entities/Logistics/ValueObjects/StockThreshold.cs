using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Logistics.ValueObjects;

public sealed record StockThreshold
{
    public decimal DangerRatio { get; }
    public decimal WarningRatio { get; }

    public StockThreshold(decimal dangerRatio, decimal warningRatio)
    {
        if (dangerRatio <= 0)
            throw new InvalidStockThresholdException("Danger ratio phải > 0.");

        if (warningRatio <= 0)
            throw new InvalidStockThresholdException("Warning ratio phải > 0.");

        if (dangerRatio >= warningRatio)
            throw new InvalidStockThresholdException("Danger ratio phải nhỏ hơn warning ratio.");

        if (warningRatio > 1)
            throw new InvalidStockThresholdException("Warning ratio không được vượt quá 1.");

        DangerRatio = dangerRatio;
        WarningRatio = warningRatio;
    }
}

public sealed class InvalidStockThresholdException(string message) : DomainException(message);
