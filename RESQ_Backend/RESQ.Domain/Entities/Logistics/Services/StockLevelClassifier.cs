using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Domain.Entities.Logistics.Services;

public static class StockLevelClassifier
{
    public static StockLevel Classify(int availableQuantity, int totalQuantity, StockThreshold threshold)
    {
        if (totalQuantity <= 0)
            return StockLevel.Unknown;

        var ratio = (decimal)availableQuantity / totalQuantity;

        if (ratio < threshold.DangerRatio)
            return StockLevel.Danger;

        if (ratio < threshold.WarningRatio)
            return StockLevel.Warning;

        return StockLevel.Safe;
    }
}
