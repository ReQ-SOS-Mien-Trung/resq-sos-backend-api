using MediatR;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

public record AcceptInvitationCommand(int TeamId, Guid UserId) : IRequest;
