using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.UnassignDepotManager;

public record UnassignDepotManagerCommand(int DepotId, Guid? RequestedBy = null) : IRequest<UnassignDepotManagerResponse>;
