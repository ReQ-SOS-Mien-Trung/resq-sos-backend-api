using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.CompleteSupplyRequest;

public record CompleteSupplyRequestCommand(int SupplyRequestId, Guid UserId, int? DepotId = null) : IRequest<CompleteSupplyRequestResponse>;
