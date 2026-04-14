using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.AcceptSupplyRequest;

public record AcceptSupplyRequestCommand(int SupplyRequestId, Guid UserId, int? DepotId = null) : IRequest<AcceptSupplyRequestResponse>;
