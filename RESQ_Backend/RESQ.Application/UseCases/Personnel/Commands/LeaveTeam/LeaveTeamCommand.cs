using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.LeaveTeam;

/// <summary>Rescuer tự rời khỏi đội cứu hộ hiện tại (soft-remove).</summary>
public class LeaveTeamCommand(Guid rescuerId) : IRequest
{
    public Guid RescuerId { get; } = rescuerId;
}
