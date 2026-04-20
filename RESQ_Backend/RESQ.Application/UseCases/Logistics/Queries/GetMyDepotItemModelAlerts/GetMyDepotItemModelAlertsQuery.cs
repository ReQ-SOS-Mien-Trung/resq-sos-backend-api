using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotItemModelAlerts;

public class GetMyDepotItemModelAlertsQuery : IRequest<PagedResult<DepotItemModelAlertDto>>
{
    public Guid UserId { get; set; }
    public int? DepotId { get; set; }
    public string? AlertType { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
