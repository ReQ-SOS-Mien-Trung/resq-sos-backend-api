using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmReturnSupplies;

public class ConfirmReturnSuppliesResponse
{
    public int ActivityId { get; set; }
    public int MissionId { get; set; }
    public int DepotId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool UsedLegacyFallback { get; set; }
    public bool DiscrepancyRecorded { get; set; }
    public List<MissionSupplyReturnExecutionItemDto> RestoredItems { get; set; } = [];
}
