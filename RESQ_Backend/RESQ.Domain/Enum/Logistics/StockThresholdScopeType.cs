using System.ComponentModel;

namespace RESQ.Domain.Enum.Logistics;

public enum StockThresholdScopeType
{
    [Description("Toàn hệ thống")]
    Global = 0,

    [Description("Theo kho")]
    Depot = 1,

    [Description("Theo danh mục trong kho")]
    DepotCategory = 2,

    [Description("Theo vật tư trong kho")]
    DepotItem = 3
}
