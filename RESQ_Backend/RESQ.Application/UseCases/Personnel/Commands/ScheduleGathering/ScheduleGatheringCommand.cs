using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.ScheduleGathering;

public record ScheduleGatheringCommand(int AssemblyPointId, DateTime AssemblyDate, DateTime CheckInDeadline, Guid CreatedBy) : IRequest<int>;
