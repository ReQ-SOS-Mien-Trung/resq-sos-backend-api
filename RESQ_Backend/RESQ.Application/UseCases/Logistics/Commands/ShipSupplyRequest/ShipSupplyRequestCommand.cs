using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.ShipSupplyRequest;

public record ShipSupplyRequestCommand(int SupplyRequestId, Guid UserId) : IRequest<ShipSupplyRequestResponse>;
