using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.CheckInAtAssemblyPoint;

public record CheckInAtAssemblyPointCommand(int AssemblyEventId, Guid UserId) : IRequest;
