using MediatR;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

public record AddTeamMemberCommand(int TeamId, Guid UserId, bool IsLeader) : IRequest;
