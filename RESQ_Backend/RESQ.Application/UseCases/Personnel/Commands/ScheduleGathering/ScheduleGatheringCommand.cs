using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.ScheduleGathering;

public record ScheduleGatheringCommand(int AssemblyPointId, DateTime AssemblyDate, Guid CreatedBy) : IRequest<int>;
