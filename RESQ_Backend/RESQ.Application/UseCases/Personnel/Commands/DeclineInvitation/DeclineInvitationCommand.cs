using MediatR;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

public record DeclineInvitationCommand(int TeamId, Guid UserId) : IRequest;