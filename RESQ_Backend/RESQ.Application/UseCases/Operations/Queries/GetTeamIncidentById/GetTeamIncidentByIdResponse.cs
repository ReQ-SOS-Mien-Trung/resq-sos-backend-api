using RESQ.Application.UseCases.Operations.Queries.Shared;

namespace RESQ.Application.UseCases.Operations.Queries.GetTeamIncidentById;

public class GetTeamIncidentByIdResponse
{
    public TeamIncidentDetailQueryDto Incident { get; set; } = default!;
}
