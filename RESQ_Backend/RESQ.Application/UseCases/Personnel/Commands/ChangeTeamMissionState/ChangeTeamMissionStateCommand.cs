using MediatR;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

public record ChangeTeamMissionStateCommand(int TeamId, string Action) : IRequest;