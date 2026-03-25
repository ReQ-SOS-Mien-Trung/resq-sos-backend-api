using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotThresholdHistory;

public class GetMyDepotThresholdHistoryQuery : IRequest<PagedResult<ThresholdHistoryItemDto>>
{
    public Guid UserId { get; set; }
    public StockThresholdScopeType? ScopeType { get; set; }
    public int? CategoryId { get; set; }
    public int? ItemModelId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
