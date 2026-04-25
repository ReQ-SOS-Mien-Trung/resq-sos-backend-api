using MediatR;

namespace RESQ.Application.UseCases.Operations.Commands.SafetyCheckIn;

public record SafetyCheckInCommand(int MissionId, int TeamId, Guid UserId) : IRequest<bool>;
