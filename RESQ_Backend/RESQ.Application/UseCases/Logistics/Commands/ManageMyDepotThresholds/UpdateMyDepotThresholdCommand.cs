using MediatR;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class UpdateMyDepotThresholdCommand : IRequest<StockThresholdCommandResponse>
{
    public Guid UserId { get; set; }
    public StockThresholdScopeType ScopeType { get; set; }
    public int? CategoryId { get; set; }
    public int? ItemModelId { get; set; }
    public decimal DangerPercent { get; set; }
    public decimal WarningPercent { get; set; }
    public uint? RowVersion { get; set; }
    public string? Reason { get; set; }
}
