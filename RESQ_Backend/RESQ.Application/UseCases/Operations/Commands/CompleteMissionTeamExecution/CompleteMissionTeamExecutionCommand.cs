using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.CompleteMissionTeamExecution;

public record CompleteMissionTeamExecutionCommand(
    int MissionId,
    int MissionTeamId,
    Guid CompletedBy,
    string? Note
) : IRequest<CompleteMissionTeamExecutionResponse>;