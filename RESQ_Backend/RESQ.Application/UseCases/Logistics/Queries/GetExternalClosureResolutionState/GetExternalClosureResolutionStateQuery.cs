using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetExternalClosureResolutionState;

public record GetExternalClosureResolutionStateQuery(int DepotId, Guid RequestingUserId)
    : IRequest<ExternalClosureResolutionStateResponse>;
