using MediatR;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

public record RemoveTeamMemberCommand(int TeamId, Guid UserId) : IRequest;