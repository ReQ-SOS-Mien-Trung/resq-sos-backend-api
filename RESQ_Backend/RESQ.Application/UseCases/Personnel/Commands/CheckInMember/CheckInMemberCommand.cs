using MediatR;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

public record CheckInMemberCommand(int TeamId, Guid UserId) : IRequest;