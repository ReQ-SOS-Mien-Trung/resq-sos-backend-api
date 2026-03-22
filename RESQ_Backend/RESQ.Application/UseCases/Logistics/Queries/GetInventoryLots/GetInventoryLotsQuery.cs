using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryLots;

public record GetInventoryLotsQuery : IRequest<PagedResult<InventoryLotDto>>
{
    public Guid UserId { get; set; }
    public int ItemModelId { get; set; }
    public int? DepotId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
