using RESQ.Application.UseCases.Operations.Queries.Shared;

namespace RESQ.Application.UseCases.Operations.Queries.GetTeamIncidents;

public class GetTeamIncidentsResponse
{
    public int MissionId { get; set; }
    public List<TeamIncidentQueryDto> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}
