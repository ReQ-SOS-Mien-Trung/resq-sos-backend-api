using MediatR;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

public record ScheduleAssemblyCommand(int TeamId, DateTime AssemblyDate) : IRequest;