using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotReusableUnits;

public record GetMyDepotReusableUnitsQuery : IRequest<PagedResult<ReusableUnitDto>>
{
    public Guid UserId { get; set; }
    public int? DepotId { get; set; }

    /// <summary>Lọc theo loại vật phẩm (ItemModelId).</summary>
    public int? ItemModelId { get; set; }

    /// <summary>Tìm kiếm theo serial number (contains, case-insensitive).</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Lọc theo trạng thái (Available, InUse, Maintenance, Decommissioned, …).</summary>
    public List<ReusableItemStatus>? Statuses { get; set; }

    /// <summary>Lọc theo tình trạng vật phẩm (Good, Fair, Poor).</summary>
    public List<ReusableItemCondition>? Conditions { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
