using MediatR;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class ResetMyDepotThresholdCommand : IRequest<StockThresholdCommandResponse>
{
    public Guid UserId { get; set; }
    public int? DepotId { get; set; }
    public StockThresholdScopeType ScopeType { get; set; }
    public int? CategoryId { get; set; }
    public int? ItemModelId { get; set; }
    public uint? RowVersion { get; set; }
    public string? Reason { get; set; }
}
