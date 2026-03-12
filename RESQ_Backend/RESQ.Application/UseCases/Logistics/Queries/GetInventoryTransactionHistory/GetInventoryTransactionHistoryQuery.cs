using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryTransactionHistory;

public class GetInventoryTransactionHistoryQuery : IRequest<PagedResult<InventoryTransactionDto>>
{
    public Guid UserId { get; set; }
    public List<InventoryActionType>? ActionTypes { get; set; }
    public List<InventorySourceType>? SourceTypes { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}