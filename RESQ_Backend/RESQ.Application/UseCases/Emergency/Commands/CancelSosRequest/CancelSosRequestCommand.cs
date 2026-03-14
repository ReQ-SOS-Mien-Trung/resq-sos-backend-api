using MediatR;

namespace RESQ.Application.UseCases.Emergency.Commands.CancelSosRequest;

public record CancelSosRequestCommand(
    int SosRequestId,
    Guid RequestedByUserId
) : IRequest<CancelSosRequestResponse>;
