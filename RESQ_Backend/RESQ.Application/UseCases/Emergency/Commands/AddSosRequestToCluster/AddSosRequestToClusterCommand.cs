using MediatR;

namespace RESQ.Application.UseCases.Emergency.Commands.AddSosRequestToCluster;

public record AddSosRequestToClusterCommand(
    int ClusterId,
    List<int> SosRequestIds,
    Guid RequestedByUserId) : IRequest<AddSosRequestToClusterResponse>;
