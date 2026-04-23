using MediatR;

namespace RESQ.Application.UseCases.Emergency.Commands.AddSosRequestToCluster;

public record AddSosRequestToClusterCommand(
    int ClusterId,
    int SosRequestId,
    Guid RequestedByUserId) : IRequest<AddSosRequestToClusterResponse>;
