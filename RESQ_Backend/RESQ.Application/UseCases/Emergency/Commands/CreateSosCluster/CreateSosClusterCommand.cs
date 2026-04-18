using MediatR;

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosCluster;

public record CreateSosClusterCommand(
    List<int> SosRequestIds,
    Guid CreatedByUserId
) : IRequest<CreateSosClusterResponse>;
