using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.DeleteDepotManager;

public record DeleteDepotManagerCommand(int DepotId) : IRequest<DeleteDepotManagerResponse>;
