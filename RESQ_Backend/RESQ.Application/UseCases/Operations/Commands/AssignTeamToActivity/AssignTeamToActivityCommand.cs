using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;

/// <summary>Giao một rescue team đang được assigned vào mission để thực hiện một activity cụ thể.</summary>
public record AssignTeamToActivityCommand(
    int ActivityId,
    int MissionId,
    int RescueTeamId,
    Guid AssignedById
) : IRequest<AssignTeamToActivityResponse>;
