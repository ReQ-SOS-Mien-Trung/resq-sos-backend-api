using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Domain.Entities.Logistics.Services;

/// <summary>
/// Pure static evaluator - không query DB, không DI.
/// Dễ unit test và tái sử dụng ở bất kỳ đâu.
/// </summary>
public static class StockWarningEvaluator
{
    /// <summary>
    /// Đánh giá mức cảnh báo tồn kho.
    /// </summary>
    /// <param name="availableQty">Số lượng khả dụng hiện tại.</param>
    /// <param name="minimumThreshold">Ngưỡng tối thiểu đã resolve. Null hoặc &lt;= 0 → UNCONFIGURED.</param>
    /// <param name="bands">Tập warning bands đã validate.</param>
    /// <param name="resolvedScope">Scope mà threshold được resolve từ.</param>
    public static StockWarningResult Evaluate(
        int availableQty,
        int? minimumThreshold,
        WarningBandSet bands,
        ThresholdResolutionScope resolvedScope = ThresholdResolutionScope.None)
    {
        if (minimumThreshold == null || minimumThreshold <= 0)
            return StockWarningResult.Unconfigured;

        var ratio = Math.Max(0m, (decimal)availableQty / minimumThreshold.Value);
        var level = bands.Match(ratio);

        return new StockWarningResult(ratio, level, resolvedScope, minimumThreshold);
    }
}
