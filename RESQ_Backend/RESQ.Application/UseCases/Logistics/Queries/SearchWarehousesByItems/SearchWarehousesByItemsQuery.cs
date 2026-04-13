using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;

/// <summary>
/// Query to search for depots that stock the requested items and can supply
/// at least <see cref="Quantity"/> units. Results are sorted by proximity to
/// the requesting manager's own depot (nearest first).
/// Pagination is applied at the item level.
/// </summary>
public record SearchWarehousesByItemsQuery : IRequest<PagedResult<ItemWarehouseAvailabilityDto>>
{
    /// <summary>
    /// List of item-model IDs to look up (e.g. [12, 45, 78]).
    /// </summary>
    public List<int>? ItemModelIds { get; set; }

    /// <summary>
    /// Minimum available quantity required for each item, keyed by ItemModelId.
    /// If an item ID is not present, a default minimum of 1 is used.
    /// </summary>
    public Dictionary<int, int> ItemQuantities { get; set; } = new();

    /// <summary>
    /// ID of the requesting manager - used to look up their depot's
    /// location so results can be sorted by proximity.
    /// Populated server-side from the JWT token.
    /// </summary>
    public Guid ManagerUserId { get; set; }

    /// <summary>
    /// When true, only depots with status "Available" or "Full" are included.
    /// Defaults to true so closed/maintenance depots are excluded.
    /// </summary>
    public bool ActiveDepotsOnly { get; set; } = true;

    /// <summary>1-based page number. Defaults to 1.</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>Number of distinct items per page. Defaults to 10, capped at 50.</summary>
    public int PageSize { get; set; } = 10;
}
