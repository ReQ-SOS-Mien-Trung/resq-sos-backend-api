using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.ConfirmSupplyRequest;

public record ConfirmSupplyRequestCommand(int SupplyRequestId, Guid UserId, int? DepotId = null) : IRequest<ConfirmSupplyRequestResponse>;
