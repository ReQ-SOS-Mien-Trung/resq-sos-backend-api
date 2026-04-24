using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.CancelAssemblyEvent;

public record CancelAssemblyEventCommand(int EventId, Guid CancelledBy) : IRequest<CancelAssemblyEventResponse>;
