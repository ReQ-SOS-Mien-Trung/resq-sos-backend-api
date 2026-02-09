using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotManagerHistory;

public record GetDepotManagerHistoryQuery(int DepotId) : IRequest<PagedResult<DepotManagerHistoryDto>>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
