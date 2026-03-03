using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public record UpdateActivityStatusCommand(
    int ActivityId,
    string Status,
    Guid DecisionBy
) : IRequest<UpdateActivityStatusResponse>;
