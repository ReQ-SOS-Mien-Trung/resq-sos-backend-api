using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventory;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotInventory;

public record GetMyDepotInventoryQuery : IRequest<PagedResult<InventoryItemDto>>
{
    public Guid UserId { get; set; }
    public List<int>? CategoryIds { get; set; }
    public List<ItemType>? ItemTypes { get; set; }
    public List<TargetGroup>? TargetGroups { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
