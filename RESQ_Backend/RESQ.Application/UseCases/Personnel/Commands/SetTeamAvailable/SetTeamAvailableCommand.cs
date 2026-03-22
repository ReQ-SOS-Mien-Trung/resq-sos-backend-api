using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.SetTeamAvailable;

public record SetTeamAvailableCommand(int TeamId, Guid LeaderUserId) : IRequest;
