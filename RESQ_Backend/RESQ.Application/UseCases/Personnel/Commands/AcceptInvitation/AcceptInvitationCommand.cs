using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.AcceptInvitation;

public record AcceptInvitationCommand(int TeamId, Guid UserId) : IRequest;
