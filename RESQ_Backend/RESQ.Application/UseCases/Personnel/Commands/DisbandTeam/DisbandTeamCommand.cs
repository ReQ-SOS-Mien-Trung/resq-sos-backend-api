using MediatR;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

public record DisbandTeamCommand(int TeamId, bool CanDisbandTeam) : IRequest;
