using MediatR;

namespace RESQ.Application.UseCases.Emergency.Commands.RemoveSosRequestFromCluster;

public record RemoveSosRequestFromClusterCommand(
    int ClusterId,
    int SosRequestId,
    Guid RequestedByUserId) : IRequest<RemoveSosRequestFromClusterResponse>;
