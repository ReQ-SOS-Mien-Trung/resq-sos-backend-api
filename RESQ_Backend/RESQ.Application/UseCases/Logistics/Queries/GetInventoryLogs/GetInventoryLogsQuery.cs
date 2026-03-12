using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryLogs;

public class GetInventoryLogsQuery : IRequest<PagedResult<InventoryLogDto>>
{
    public int? DepotId { get; set; }
    public int? ReliefItemId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}