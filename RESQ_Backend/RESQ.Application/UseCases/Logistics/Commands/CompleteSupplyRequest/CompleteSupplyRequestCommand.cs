using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.CompleteSupplyRequest;

public record CompleteSupplyRequestCommand(int SupplyRequestId, Guid UserId) : IRequest<CompleteSupplyRequestResponse>;
