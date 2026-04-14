using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.PrepareSupplyRequest;

public record PrepareSupplyRequestCommand(int SupplyRequestId, Guid UserId, int? DepotId = null) : IRequest<PrepareSupplyRequestResponse>;
