using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotInventory;

public record GetDepotInventoryQuery : IRequest<PagedResult<InventoryItemDto>>
{
    public int DepotId { get; set; }
    public List<int>? CategoryIds { get; set; }
    public List<ItemType>? ItemTypes { get; set; }
    public List<TargetGroup>? TargetGroups { get; set; }
    /// <summary>Tìm kiếm theo tên vật phẩm (không phân biệt hoa thường).</summary>
    public string? ItemName { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
