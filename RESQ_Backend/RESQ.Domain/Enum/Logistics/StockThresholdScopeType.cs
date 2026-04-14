using System.ComponentModel;

namespace RESQ.Domain.Enum.Logistics;

public enum StockThresholdScopeType
{
    [Description("Toàn h? th?ng")]
    Global = 0,

    [Description("Theo kho")]
    Depot = 1,

    [Description("Theo danh m?c trong kho")]
    DepotCategory = 2,

    [Description("Theo v?t ph?m trong kho")]
    DepotItem = 3
}
