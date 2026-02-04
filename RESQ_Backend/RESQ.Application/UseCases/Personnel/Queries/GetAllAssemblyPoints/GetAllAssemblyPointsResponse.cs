using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAllAssemblyPoints;

public class GetAllAssemblyPointsResponse
{
    public List<AssemblyPointDto> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}
