using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.AssignDepotManager;

public record AssignDepotManagerCommand(int DepotId, Guid ManagerId) : IRequest<AssignDepotManagerResponse>;
