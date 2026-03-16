using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.ConfirmSupplyRequest;

public record ConfirmSupplyRequestCommand(int SupplyRequestId, Guid UserId) : IRequest<ConfirmSupplyRequestResponse>;
