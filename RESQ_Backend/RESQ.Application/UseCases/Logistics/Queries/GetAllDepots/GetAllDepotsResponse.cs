using RESQ.Application.UseCases.Logistics.Queries.GetAllDepots.Depot;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAllDepots;

public class GetAllDepotsResponse
{
    public List<DepotDto> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}
