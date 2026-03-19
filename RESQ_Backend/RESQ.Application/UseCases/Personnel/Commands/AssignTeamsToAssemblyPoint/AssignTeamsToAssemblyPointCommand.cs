using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.AssignTeamsToAssemblyPoint;

public record AssignTeamsToAssemblyPointCommand(int AssemblyPointId, List<int> TeamIds) : IRequest;
