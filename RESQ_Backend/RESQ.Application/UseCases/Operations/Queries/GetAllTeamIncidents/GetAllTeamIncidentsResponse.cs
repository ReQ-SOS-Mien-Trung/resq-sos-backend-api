using RESQ.Application.UseCases.Operations.Queries.Shared;

namespace RESQ.Application.UseCases.Operations.Queries.GetAllTeamIncidents;

public class GetAllTeamIncidentsResponse
{
    public List<TeamIncidentQueryDto> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}
